using Neo4j.Driver;
using Npc.Api.Dtos;

namespace Npc.Api.Repositories
{
    public interface IConversationRepository
    {
        Task<ConversationResponse> CreateConversationAsync(string title, CancellationToken ct = default);
        Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default);
        Task<UtteranceResponse> AddRootUtteranceAsync(Guid conversationId, string text, Guid? characterId, CancellationToken ct = default);
        Task<UtteranceResponse> AddNextUtteranceAsync(Guid fromUtteranceId, string text, Guid? characterId, CancellationToken ct = default);
        Task<UtteranceDetail?> GetUtteranceAsync(Guid utteranceId, CancellationToken ct = default);
        Task<UtteranceDetail?> UpdateUtteranceAsync(Guid utteranceId, string text, string[]? tags, int expectedVersion, CancellationToken ct = default);
        Task<bool> DeleteUtteranceAsync(Guid utteranceId, CancellationToken ct = default);
        Task<PathResponse?> GetLinearPathAsync(Guid conversationId, CancellationToken ct = default);
        Task<GraphResponse?> GetGraphAsync(Guid conversationId, int depth, CancellationToken ct = default);
        Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct = default);
        Task SetBranchWeightAsync(Guid fromUtteranceId, Guid toUtteranceId, double weight, CancellationToken ct = default);
    }

    public class ConversationRepository : IConversationRepository
    {
        private readonly IDriver _driver;

        public ConversationRepository(IDriver driver)
        {
            _driver = driver;
        }

        public async Task<ConversationResponse> CreateConversationAsync(string title, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            const string cypher = """
                CREATE (c:Conversation {id:$id, title:$title, createdAt:datetime()})
                RETURN c.id AS id, c.title AS title
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var record = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = id.ToString(), title });
                return await cursor.SingleAsync();
            });

            return new ConversationResponse(Guid.Parse(record["id"].As<string>()), record["title"].As<string>());
        }

        public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
        {
            const string cypher = """
                MATCH (c:Conversation {id:$cid})
                RETURN c.id AS id, c.title AS title
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var record = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
                return await cursor.SingleAsync();
            });

            if (record == null) return null;
            return new ConversationResponse(Guid.Parse(record["id"].As<string>()), record["title"].As<string>());
        }

        public async Task<UtteranceResponse> AddRootUtteranceAsync(Guid conversationId, string text, Guid? characterId, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            const string cypher = """
                MATCH (c:Conversation {id:$cid})
                MERGE (c)-[:ROOT]->(u:Utterance {id:$id})
                SET u.text = $text,
                    u.characterId = $charId,
                    u.createdAt = coalesce(u.createdAt, datetime()),
                    u.updatedAt = datetime(),
                    u.deleted = coalesce(u.deleted, false),
                    u.version = coalesce(u.version, 1),
                    u.tags = coalesce(u.tags, [])
                RETURN u.id AS id, u.text AS text, u.characterId AS characterId
                """;

            return await ExecuteUtteranceQuery(cypher, new
            {
                cid = conversationId.ToString(),
                id = id.ToString(),
                text,
                charId = characterId?.ToString()
            });
        }

        public async Task<UtteranceResponse> AddNextUtteranceAsync(Guid fromUtteranceId, string text, Guid? characterId, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            const string cypher = """
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

            return await ExecuteUtteranceQuery(cypher, new
            {
                fromId = fromUtteranceId.ToString(),
                id = id.ToString(),
                text,
                charId = characterId?.ToString()
            });
        }

        public async Task<UtteranceDetail?> GetUtteranceAsync(Guid utteranceId, CancellationToken ct = default)
        {
            const string cypher = """
                MATCH (u:Utterance {id:$id})
                RETURN u.id AS id, u.text AS text, u.characterId AS characterId,
                    coalesce(u.deleted,false) AS deleted,
                    coalesce(u.version,1) AS version,
                    coalesce(u.tags,[]) AS tags
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var record = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = utteranceId.ToString() });
                return await cursor.SingleAsync();
            });

            if (record == null) return null;
            return MapUtteranceDetail(record);
        }

        public async Task<UtteranceDetail?> UpdateUtteranceAsync(Guid utteranceId, string text, string[]? tags, int expectedVersion, CancellationToken ct = default)
        {
            const string cypher = """
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

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var record = await session.ExecuteWriteAsync(async tx =>
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

            if (record == null) return null;
            return MapUtteranceDetail(record);
        }

        public async Task<bool> DeleteUtteranceAsync(Guid utteranceId, CancellationToken ct = default)
        {
            const string cypher = """
                MATCH (u:Utterance {id:$id})
                WHERE coalesce(u.deleted,false)=false
                SET u.deleted = true,
                    u.updatedAt = datetime()
                RETURN u.id AS id
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var record = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { id = utteranceId.ToString() });
                return await cursor.SingleAsync();
            });

            return record != null;
        }

        public async Task<PathResponse?> GetLinearPathAsync(Guid conversationId, CancellationToken ct = default)
        {
            const string cypher = """
                MATCH (c:Conversation {id:$cid})-[:ROOT]->(root:Utterance)
                WHERE coalesce(root.deleted,false)=false
                OPTIONAL MATCH p=(root)-[:NEXT*0..25]->(last)
                WITH c, [n IN nodes(p) WHERE coalesce(n.deleted,false)=false] AS ns
                RETURN c.id AS cid, c.title AS title,
                    [n IN ns | { id:n.id, text:n.text, characterId:n.characterId }] AS seq
                LIMIT 1
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var record = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
                return await cursor.SingleAsync();
            });

            if (record == null) return null;

            var seq = record["seq"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                return new UtteranceResponse(
                    Guid.Parse(m["id"]!.ToString()!),
                    m["text"]?.ToString() ?? "",
                    ParseGuid(m["characterId"]?.ToString())
                );
            }).ToArray();

            return new PathResponse(Guid.Parse(record["cid"].As<string>()), record["title"].As<string>(), seq);
        }

        public async Task<GraphResponse?> GetGraphAsync(Guid conversationId, int depth, CancellationToken ct = default)
        {
            if (depth is < 1 or > 25) depth = 10;

            const string cypher = """
                MATCH (c:Conversation {id:$cid})
                OPTIONAL MATCH (c)-[:ROOT]->(root:Utterance)
                WITH c, root
                CALL {
                WITH root
                OPTIONAL MATCH p=(root)-[:NEXT|BRANCH_TO*0..$depth]->(u:Utterance)
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

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var record = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString(), depth = depth });
                return await cursor.SingleAsync();
            });

            if (record == null) return null;

            var nodes = record["ns"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                Guid.TryParse(m["id"]?.ToString(), out var id);
                Guid? charId = ParseGuid(m["characterId"]?.ToString());
                var tags = (m["tags"] as IEnumerable<object> ?? Array.Empty<object>())
                    .Select(t => t?.ToString() ?? "")
                    .ToArray();
                return new UtteranceNode(id, m["text"]?.ToString() ?? "", charId, (bool)m["deleted"], tags);
            }).ToArray();

            var rels = record["rels"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                Guid.TryParse(m["from"]?.ToString(), out var from);
                Guid.TryParse(m["to"]?.ToString(), out var to);
                double? weight = null;
                if (m.TryGetValue("weight", out var w) && w is not null && double.TryParse(w.ToString(), out var dw))
                    weight = dw;
                return new RelationEdge(from, to, m["type"]?.ToString() ?? "", weight);
            }).ToArray();

            return new GraphResponse(
                Guid.Parse(record["cid"].As<string>()),
                record["title"].As<string>(),
                nodes,
                rels
            );
        }

        public async Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct = default)
        {
            const string cypher = """
                MATCH (a:Utterance {id:$fromId}), (b:Utterance {id:$toId})
                WHERE coalesce(a.deleted,false)=false AND coalesce(b.deleted,false)=false
                MERGE (a)-[r:BRANCH_TO]->(b)
                ON CREATE SET r.weight = 1.0
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
            {
                fromId = fromUtteranceId.ToString(),
                toId = toUtteranceId.ToString()
            }));
        }

        public async Task SetBranchWeightAsync(Guid fromUtteranceId, Guid toUtteranceId, double weight, CancellationToken ct = default)
        {
            if (weight <= 0) weight = 0.01;

            const string cypher = """
                MATCH (a:Utterance {id:$fromId})-[r:BRANCH_TO]->(b:Utterance {id:$toId})
                SET r.weight = $w
                RETURN r.weight AS w
                """;

            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    fromId = fromUtteranceId.ToString(),
                    toId = toUtteranceId.ToString(),
                    w = weight
                });
                return await cursor.SingleAsync();
            });
        }

        private async Task<UtteranceResponse> ExecuteUtteranceQuery(string cypher, object parameters)
        {
            await using var session = _driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var record = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                return await cursor.SingleAsync();
            });

            return new UtteranceResponse(
                Guid.Parse(record["id"].As<string>()),
                record["text"].As<string>(),
                ParseGuid(record["characterId"]?.As<string>())
            );
        }

        private static UtteranceDetail MapUtteranceDetail(IRecord record)
        {
            var tagsList = record["tags"].As<List<object>>().Select(t => t.ToString() ?? "").ToArray();
            return new UtteranceDetail(
                Guid.Parse(record["id"].As<string>()),
                record["text"].As<string>(),
                ParseGuid(record["characterId"]?.As<string>()),
                record["deleted"].As<bool>(),
                Convert.ToInt32(record["version"].As<long>()),
                tagsList
            );
        }

        private static Guid? ParseGuid(string? value) =>
            Guid.TryParse(value, out var guid) ? guid : null;
    }
}