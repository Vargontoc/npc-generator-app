using Neo4j.Driver;
using Npc.Api.Dtos;

namespace Npc.Api.Services.Impl
{
    public class ConversationGraphService(IDriver driver) : IConversationGraphService
    {
        public async Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct)
        {
            var cypher = """
                MATCH (a:Utterance {id:$fromId}), (b:Utterance {id:$toId})
                WHERE coalesce(a.deleted,false)=false AND coalesce(b.deleted,false)=false
                MERGE (a)-[:BRANCH_TO]->(b)
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
            {
                fromId = fromUtteranceId.ToString(),
                toId = toUtteranceId.ToString()
            }));
        }

        public async Task<UtteranceResponse> AddNextUtterance(Guid fromUtteranceId, string text, Guid? characterId, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var cypher = """
                MATCH (u1:Utterance {id:$fromId}) WHERE coalesce(u1.deleted,false)=false
                CREATE (u2:Utterance {
                    id:$id,
                    text:$text,
                    characterId:$charId,
                    createdAt:datetime(),
                    updatedAt:datetime(),
                    deleted:false,
                    version:1,
                    tags:[]
                })
                CREATE (u1)-[:NEXT]->(u2)
                RETURN u2.id AS id, u2.text AS text, u2.characterId AS characterId
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    fromId = fromUtteranceId.ToString(),
                    id = id.ToString(),
                    text,
                    charId = characterId?.ToString()
                });
                return await cursor.SingleAsync();
            });
            return new UtteranceResponse(Guid.Parse(rec["id"].As<string>()), rec["text"].As<string>(), ParseGuid(rec["characterId"]?.As<string>()));
        }



        public async Task<UtteranceResponse> AddRootUtteranceAsync(Guid conversationId, string text, Guid? characterId, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var cypher = """
                MATCH (c:Conversation {id:$cid})
                MERGE (c)-[:ROOT]->(u:Utterance {id:$id})
                SET u.text=$text,
                    u.characterId=$charId,
                    u.createdAt = coalesce(u.createdAt, datetime()),
                    u.updatedAt = datetime(),
                    u.deleted = coalesce(u.deleted,false),
                    u.version = coalesce(u.version,1),
                    u.tags = coalesce(u.tags,[])
                RETURN u.id AS id, u.text AS text, u.characterId AS characterId
                """;
            return await RunCreateUtterance(cypher, conversationId, id, text, characterId);
        }

        public async Task<ConversationResponse> CreateConversationAsync(string title, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var cypher = """
                CREATE (c:Conversation {id:$id, title:$title, createdAt:datetime()})
                RETURN c.id AS id, c.title AS title
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = id.ToString(), title });
                return await cursor.SingleAsync();
            });
            return new ConversationResponse(Guid.Parse(rec["id"].As<string>()), rec["title"].As<string>());
        }

        public async Task<PathResponse?> GetLinearPathAsync(Guid conversationId, CancellationToken ct)
        {
            var cypher = """
                MATCH (c:Conversation {id:$cid})-[:ROOT]->(root:Utterance)
                WHERE coalesce(root.deleted,false)=false
                OPTIONAL MATCH p=(root)-[:NEXT*0..25]->(last)
                WITH c, [n IN nodes(p) WHERE coalesce(n.deleted,false)=false] AS ns
                RETURN c.id AS cid, c.title AS title,
                    [n IN ns | { id:n.id, text:n.text, characterId:n.characterId }] AS seq
                LIMIT 1
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var rec = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
                return await cursor.SingleAsync();
            });
            if (rec is null) return null;

            var seq = rec["seq"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                return new UtteranceResponse(
                    Guid.Parse(m["id"]!.ToString()!),
                    m["text"]?.ToString() ?? "",
                    ParseGuid(m["characterId"]?.ToString())
                );
            }).ToArray();

            return new PathResponse(Guid.Parse(rec["cid"].As<string>()), rec["title"].As<string>(), seq);
        }

        public async Task<UtteranceDetail?> GetUtteranceAsync(Guid utteranceId, CancellationToken ct)
        {
            var cypher = """
                    MATCH (u:Utterance {id:$id})
                    RETURN u.id AS id, u.text AS text, u.characterId AS characterId,
                        coalesce(u.deleted,false) AS deleted,
                        coalesce(u.version,1) AS version,
                        coalesce(u.tags,[]) AS tags
            """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var rec = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = utteranceId.ToString() });
                return await cursor.SingleAsync();
            });
            if (rec is null) return null;

            return MapDetail(rec);
        }
        public async Task<bool> SoftDeleteUtteranceAsync(Guid utteranceId, CancellationToken ct)
        {
            var cypher = """
                MATCH (u:Utterance {id:$id})
                WHERE coalesce(u.deleted,false)=false
                SET u.deleted = true,
                    u.updatedAt = datetime()
                RETURN u.id AS id
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = utteranceId.ToString() });
                return await cursor.SingleAsync();
            });
            return rec is not null;
        }

        public async Task<UtteranceDetail?> UpdateUtteranceAsync(Guid utteranceId, string text, string[]? tags, int expectedVersion, CancellationToken ct)
        {
            var cypher = """
                MATCH (u:Utterance {id:$id})
                WHERE coalesce(u.deleted,false)=false AND coalesce(u.version,1) = $expectedVersion
                SET u.text = $text,
                    u.tags = coalesce($tags, []),
                    u.version = coalesce(u.version,1) + 1,
                    u.updatedAt = datetime()
                RETURN u.id AS id, u.text AS text, u.characterId AS characterId,
                    coalesce(u.deleted,false) AS deleted,
                    u.version AS version,
                    coalesce(u.tags,[]) AS tags
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    id = utteranceId.ToString(),
                    text,
                    tags,
                    expectedVersion
                });
                return await cursor.SingleAsync();
            });
            if (rec is null) return null;
            return MapDetail(rec);
        }
        
        public async Task<GraphResponse?> GetGraphAsync(Guid conversationId, int depth, CancellationToken ct)
        {
            if (depth is < 1 or > 25) depth = 10;

            var cypher = """
            MATCH (c:Conversation {id:$cid})
            OPTIONAL MATCH (c)-[:ROOT]->(root:Utterance)
            WITH c, root
            CALL {
            WITH root
            OPTIONAL MATCH p=(root)-[:NEXT|BRANCH_TO*0..
            """ + depth + """
            ]->(u:Utterance)
            WITH root, collect(DISTINCT u) AS collectedNodes
            WITH CASE 
                WHEN root IS NOT NULL 
                THEN [root] + collectedNodes 
                ELSE collectedNodes 
            END AS rawNodes
            WITH [n IN rawNodes WHERE n IS NOT NULL] AS nodes
            CALL {
                WITH nodes
                UNWIND nodes AS a
                MATCH (a)-[r:NEXT|BRANCH_TO]->(b)
                WHERE b IN nodes
                RETURN collect(DISTINCT { 
                from: a.id, 
                to: b.id, 
                type: type(r), 
                weight: r.weight 
                }) AS rels
            }
            RETURN nodes, rels
            }
            RETURN c.id AS cid,
                c.title AS title,
                [n IN nodes | {
                    id: n.id,
                    text: n.text,
                    characterId: n.characterId,
                    deleted: coalesce(n.deleted, false),
                    tags: coalesce(n.tags, [])
                }] AS ns,
                rels
            """;


            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var record = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString(), cdepth = depth });
                return await cursor.SingleAsync();
            });

            if (record is null) return null;

            var nodes = record["ns"]
                .As<List<object>>()
                .Select(o =>
                {
                    var m = (IDictionary<string, object>)o;
                    Guid.TryParse(m["id"]?.ToString(), out var id);
                    Guid? charId = Guid.TryParse(m["characterId"]?.ToString(), out var cc) ? cc : null;
                    var tags = (m["tags"] as IEnumerable<object> ?? Array.Empty<object>())
                        .Select(t => t?.ToString() ?? "")
                        .ToArray();
                    return new UtteranceNode(id, m["text"]?.ToString() ?? "", charId, (bool)m["deleted"], tags);
                })
                .ToArray();

            var rels = record["rels"]
                .As<List<object>>()
                .Select(o =>
                {
                    var m = (IDictionary<string, object>)o;
                    Guid.TryParse(m["from"]?.ToString(), out var from);
                    Guid.TryParse(m["to"]?.ToString(), out var to);
                    double? weight = null;
                    if (m.TryGetValue("weight", out var w) && w is not null)
                    {
                        if (double.TryParse(w.ToString(), out var dw))
                            weight = dw;
                    }
                    return new RelationEdge(from, to, m["type"]?.ToString() ?? "", weight);
                })
                .ToArray();

            return new GraphResponse(
                Guid.Parse(record["cid"].As<string>()),
                record["title"].As<string>(),
                nodes,
                rels
            );
        }
        private async Task<UtteranceResponse> RunCreateUtterance(string cypher, Guid conversationId, Guid id, string text, Guid? characterId)
        {
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    cid = conversationId.ToString(),
                    id = id.ToString(),
                    text,
                    charId = characterId?.ToString()
                });
                return await cursor.SingleAsync();
            });
            return new UtteranceResponse(Guid.Parse(rec["id"].As<string>()), rec["text"].As<string>(), ParseGuid(rec["characterId"]?.As<string>()));
        }
        private async Task<UtteranceResponse> RunUtteranceByFrom(string cypher, Guid fromUtteranceId, Guid id, string text, Guid? characterId)
        {

            await using var session = driver.AsyncSession();
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    fromId = fromUtteranceId.ToString(),
                    id = id.ToString(),
                    text,
                    charId = characterId?.ToString()
                });
                return await cursor.SingleAsync();
            });
            Guid.TryParse(rec["id"].As<string>(), out var uid);
            Guid? charId = Guid.TryParse(rec["characterId"]?.As<string>(), out var cc) ? cc : null;
            return new UtteranceResponse(uid, rec["text"].As<string>(), charId);
        }
        
        private static Guid? ParseGuid(string? v) =>
            Guid.TryParse(v, out var g) ? g : null;

        private static UtteranceDetail MapDetail(IRecord rec)
        {
            var tagsList = rec["tags"].As<List<object>>().Select(t => t.ToString() ?? "").ToArray();


            return new UtteranceDetail(
                Guid.Parse(rec["id"].As<string>()),
                rec["text"].As<string>(),
                ParseGuid(rec["characterId"]?.As<string>()),
                rec["deleted"].As<bool>(),
                Convert.ToInt32(rec["version"].As<long>()),
                tagsList
            );
        }

    }
}