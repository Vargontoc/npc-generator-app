using System.Text;
using Neo4j.Driver;
using Npc.Api.Dtos;

namespace Npc.Api.Services.Impl
{
    public class ConversationGraphService(IDriver driver, IAgentConversationService agent) : IConversationGraphService
    {
        public async Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct)
        {
            const string cypher = """
                MATCH (a:Utterance {id:$fromId}), (b:Utterance {id:$toId})
                WHERE coalesce(a.deleted,false)=false AND coalesce(b.deleted,false)=false
                MERGE (a)-[r:BRANCH_TO]->(b)
                ON CREATE SET r.weight = 1.0
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new {
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

        public async Task<PathResponse?> GetRandomPathAsync(Guid conversationId, int maxDepth, CancellationToken ct)
        {
            if (maxDepth is < 1 or > 50) maxDepth = 20;

    // Traemos subgrafo (nodos + relaciones) igual que graph pero sin profundizar infinito
    var cypher = """
        MATCH (c:Conversation {id:$cid})
        OPTIONAL MATCH (c)-[:ROOT]->(root:Utterance)
        WITH c, root
        OPTIONAL MATCH p=(root)-[:NEXT|BRANCH_TO*0..50]->(u:Utterance)
        WITH c, root, collect(DISTINCT u) AS us
        WITH c, CASE WHEN root IS NULL THEN us ELSE [root] + us END AS rawNodes
        WITH c, [n IN rawNodes WHERE n IS NOT NULL] AS nodes
        OPTIONAL MATCH (a:Utterance)-[r:NEXT|BRANCH_TO]->(b:Utterance)
        WHERE a IN nodes AND b IN nodes
        RETURN c.id AS cid,
               c.title AS title,
               nodes,
               collect({ from:a.id, to:b.id, type:type(r), weight:r.weight }) AS rels
        """;

    await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
    var record = await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
        return await cursor.SingleAsync();
    });
    if (record is null) return null;

    // Map nodes
    var nodeList = record["nodes"].As<List<object>>().Select(o =>
    {
        var m = (IDictionary<string, object>)o;
        Guid.TryParse(m["id"]?.ToString(), out var id);
        return new {
            Id = id,
            Text = m["text"]?.ToString() ?? "",
            CharacterId = ParseGuid(m["characterId"]?.ToString()),
            Deleted = (bool?)m["deleted"] ?? false
        };
    }).Where(n => !n.Deleted).ToDictionary(n => n.Id);

    // Relaciones
    var edges = record["rels"].As<List<object>>().Select(o =>
    {
        var m = (IDictionary<string, object>)o;
        Guid.TryParse(m["from"]?.ToString(), out var from);
        Guid.TryParse(m["to"]?.ToString(), out var to);
        var type = m["type"]?.ToString() ?? "";
        var weight = 1.0;
        if (type == "BRANCH_TO" && m.TryGetValue("weight", out var w) && w is not null && double.TryParse(w.ToString(), out var dw))
            weight = dw <= 0 ? 0.01 : dw;
        return new { From = from, To = to, Type = type, Weight = weight };
    }).ToList();

    // Encontrar root
    // Root = nodo que tiene relación :ROOT desde Conversation (garantizado que llega en nodes si existe)
    // Alternativa: nodo sin entradas NEXT desde otro
    Guid? rootId = null;
    if (nodeList.Count > 0)
    {
        var targets = edges.Where(e => e.Type == "NEXT").Select(e => e.To).ToHashSet();
        rootId = nodeList.Keys.FirstOrDefault(k => !targets.Contains(k));
        if (rootId == Guid.Empty) rootId = nodeList.Keys.First();
    }
    if (rootId is null) return new PathResponse(conversationId, record["title"].As<string>(), Array.Empty<UtteranceResponse>());

    var path = new List<UtteranceResponse>();
    var current = rootId.Value;
    var depth = 0;
    var rng = Random.Shared;

    while (depth < maxDepth && nodeList.ContainsKey(current))
    {
        var n = nodeList[current];
        path.Add(new UtteranceResponse(n.Id, n.Text, n.CharacterId));

        // Siguiente directo (NEXT)
        var nexts = edges.Where(e => e.Type == "NEXT" && e.From == current).Select(e => e.To).ToList();
        var branches = edges.Where(e => e.Type == "BRANCH_TO" && e.From == current).ToList();

        if (nexts.Count == 0 && branches.Count == 0)
            break;

        if (nexts.Count == 1 && branches.Count == 0)
        {
            current = nexts[0];
        }
        else
        {
            // Mezclar: prioridad a NEXT si existe, pero podemos permitir que branches compitan:
            var candidates = new List<(Guid to, double w)>();

            foreach (var nTo in nexts)
                candidates.Add((nTo, 1.0)); // peso base

            foreach (var b in branches)
                candidates.Add((b.To, b.Weight));

            var sum = candidates.Sum(c => c.w);
            if (sum <= 0) break;
            var roll = rng.NextDouble() * sum;
            double acc = 0;
            Guid chosen = candidates[0].to;
            foreach (var c in candidates)
            {
                acc += c.w;
                if (roll <= acc)
                {
                    chosen = c.to;
                    break;
                }
            }
            current = chosen;
        }

        depth++;
    }

    return new PathResponse(conversationId, record["title"].As<string>(), path.ToArray());
        }

        public async Task SetBranchWeightAsync(Guid fromUtteranceId, Guid toUtteranceId, double? weight, CancellationToken ct)
        {
             if (weight <= 0) weight = 0.01;
            const string cypher = """
                MATCH (a:Utterance {id:$fromId})-[r:BRANCH_TO]->(b:Utterance {id:$toId})
                SET r.weight = $w
                RETURN r.weight AS w
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new {
                    fromId = fromUtteranceId.ToString(),
                    toId = toUtteranceId.ToString(),
                    w = weight
                });
                return await cursor.SingleAsync();
            }) ?? throw new InvalidOperationException("Branch relation not found");
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

        public async Task<ConversationResponse> ImportConversationAsync(ConversationImportRequest req, CancellationToken ct)
        {
                   var convId = req.ConversationId ?? Guid.NewGuid();
            var title = req.Title ?? $"Conversation {convId}";
            var utters = req.Utterances ?? Array.Empty<ImportedUtterance>();
            var rels = req.Relations ?? Array.Empty<ImportedRelation>();

            // Validate relation types
            if (rels.Any(r => r.Type is not ("NEXT" or "BRANCH_TO" or "ROOT")))
                throw new ArgumentException("Invalid relation type detected.");

            // Build Cypher
            var sb = new StringBuilder();
            sb.AppendLine("MERGE (c:Conversation {id:$cid})");
            sb.AppendLine("SET c.title = $title, c.importedAt = datetime()");
            sb.AppendLine("WITH c");
            sb.AppendLine("CALL {");

            // Create utterances
            int idx = 0;
            foreach (var u in utters)
            {
                var nodeVar = $"u{idx}";
                var nodeId = u.Id is not null && req.PreserveIds ? u.Id.Value : Guid.NewGuid();
                sb.AppendLine($"""
                    MERGE ({nodeVar}:Utterance id:'{nodeId}')
                    SET {nodeVar}.text = $p{idx}_text,
                        {nodeVar}.characterId = $p{idx}_char,
                        {nodeVar}.deleted = coalesce($p{idx}_del,false),
                        {nodeVar}.version = coalesce($p{idx}_ver,1),
                        {nodeVar}.tags = coalesce($p{idx}_tags,[]),
                        {nodeVar}.updatedAt = datetime(),
                        {nodeVar}.createdAt = coalesce({nodeVar}.createdAt, datetime())
                    """);
                idx++;
            }

            sb.AppendLine("RETURN 1 AS done }");
            sb.AppendLine("WITH c");
            sb.AppendLine("CALL {");

            // Relations
            for (int i = 0; i < rels.Length; i++)
            {
                var r = rels[i];
                if (r.Type == "ROOT")
                {
                    sb.AppendLine($"""
                        MATCH (c:Conversation id:$cid),(ru:Utterance id:'{r.To}')
                        MERGE (c)-[:ROOT]->(ru)
                        """);
                }
                else
                {
                    sb.AppendLine($"""
                        MATCH (fa:Utterance id:'{r.From}'),(ta:Utterance d:'{r.To}')
                        MERGE (fa)-[rel:{r.Type}]->(ta)
                        """);
                    if (r.Type == "BRANCH_TO")
                    {
                        var w = r.Weight is null or <= 0 ? 1.0 : r.Weight.Value;
                        sb.AppendLine($"SET rel.weight = {w}");
                    }
                }
            }

            sb.AppendLine("RETURN 1 AS done }");
            sb.AppendLine("RETURN c.id AS id, c.title AS title");

            // Parameters
            var parameters = new Dictionary<string, object?>
            {
                ["cid"] = convId.ToString(),
                ["title"] = title
            };

            // Param values for utterances
            for (int i = 0; i < utters.Length; i++)
            {
                var u = utters[i];
                parameters[$"p{i}_text"] = u.Text;
                parameters[$"p{i}_char"] = u.CharacterId?.ToString();
                parameters[$"p{i}_del"] = u.Deleted ?? false;
                parameters[$"p{i}_ver"] = u.Version ?? 1;
                parameters[$"p{i}_tags"] = u.Tags ?? Array.Empty<string>();
            }

            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            var rec = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(sb.ToString(), parameters);
                return await cursor.SingleAsync();
            });

            return new ConversationResponse(Guid.Parse(rec["id"].As<string>()), rec["title"].As<string>());
        }

        public async Task<ConversationExportResponse?> ExportConversationAsync(Guid conversationId, int depth, CancellationToken ct)
        {
             if (depth is < 1 or > 50) depth = 25;
            var cypher = """
                MATCH (c:Conversation {id:$cid})
                OPTIONAL MATCH (c)-[:ROOT]->(root:Utterance)
                WITH c, root
                CALL {
                WITH root
                OPTIONAL MATCH p=(root)-[:NEXT|BRANCH_TO*0..
                """ + depth
                +

                """
                ]->(u:Utterance)
                WITH collect(DISTINCT u) + root AS rawNodes
                WITH [n IN rawNodes WHERE n IS NOT NULL] AS nodes, root
                RETURN nodes, root
                }
                WITH c, root, nodes
                UNWIND nodes AS n
                WITH c, root, collect(DISTINCT 
                    id:n.id,
                    text:n.text,
                    characterId:n.characterId,
                    deleted:coalesce(n.deleted,false),
                    tags:coalesce(n.tags,[]),
                    version:coalesce(n.version,1)
                ) AS utters, nodes
                CALL {
                WITH nodes
                UNWIND nodes AS a
                MATCH (a)-[r:NEXT|BRANCH_TO]->(b)
                WHERE b IN nodes
                RETURN collect(DISTINCT  from:a.id, to:b.id, type:type(r), weight:r.weight ) AS rels
                }
                RETURN c.id AS cid,
                    c.title AS title,
                    root.id AS rootId,
                    utters,
                    rels
                """;
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
            var rec = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
                return await cursor.SingleAsync();
            });
            if (rec is null) return null;

            var utters = rec["utters"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                Guid.TryParse(m["id"]?.ToString(), out var id);
                Guid? charId = ParseGuid(m["characterId"]?.ToString());
                var tags = (m["tags"] as IEnumerable<object> ?? Array.Empty<object>()).Select(t => t?.ToString() ?? "").ToArray();
                var deleted = m["deleted"] is bool b && b;
                var ver = Convert.ToInt32(m["version"]);
                return new ImportedUtterance(id, m["text"]?.ToString() ?? "", charId, deleted, tags, ver);
            }).ToArray();

            var rels = new List<ImportedRelation>();
            // ROOT relation
            Guid? rootId = ParseGuid(rec["rootId"]?.As<string>());
            if (rootId != null)
                rels.Add(new ImportedRelation(conversationId, rootId.Value, "ROOT", null));

            // Other relations
            foreach (var o in rec["rels"].As<List<object>>())
            {
                var m = (IDictionary<string, object>)o;
                Guid.TryParse(m["from"]?.ToString(), out var from);
                Guid.TryParse(m["to"]?.ToString(), out var to);
                var type = m["type"]?.ToString() ?? "";
                double? weight = null;
                if (type == "BRANCH_TO" && m.TryGetValue("weight", out var w) && w is not null && double.TryParse(w.ToString(), out var dw))
                    weight = dw;
                rels.Add(new ImportedRelation(from, to, type, weight));
            }

            return new ConversationExportResponse(
                conversationId,
                rec["title"].As<string>(),
                rootId,
                utters,
                rels.ToArray()
            );
        }

        public async Task<GeneratedUtterance[]> AutoExpandedAsync(Guid conversationId, AutoExpandedRequest req, CancellationToken ct)
        {
            if (agent is null) return [];

            var generated = await agent.GenerateAsync(conversationId, req, ct);
            if (generated.Length == 0) return generated;

            // Attach to specified node or root
            var fromId = req.FromUtteranceId;

            // If no fromId: find root
            if (fromId is null)
            {
                const string findRoot = """
                    MATCH (c:Conversation {id:$cid})-[:ROOT]->(u:Utterance)
                    RETURN u.id AS id
                    """;
                await using var sessionRoot = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Read));
                var rootRec = await sessionRoot.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(findRoot, new { cid = conversationId.ToString() });
                    return await cursor.SingleAsync();
                });
                if (rootRec is null) return generated; // no root yet
                fromId = Guid.Parse(rootRec["id"].As<string>());
            }

            // Insert each as NEXT chain
            Guid current = fromId!.Value;
            foreach (var gen in generated)
            {
                var newId = Guid.NewGuid();
                const string cypher = """
                    MATCH (u:Utterance {id:$fromId})
                    CREATE (n:Utterance {
                        id:$id,
                        text:$text,
                        characterId:$charId,
                        createdAt:datetime(),
                        updatedAt:datetime(),
                        deleted:false,
                        version:1,
                        tags:coalesce($tags,[])
                    })
                    CREATE (u)-[:NEXT]->(n)
                    RETURN n.id AS id
                    """;
                await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
                var _ = await session.ExecuteWriteAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(cypher, new
                    {
                        fromId = current.ToString(),
                        id = newId.ToString(),
                        text = gen.Text,
                        charId = gen.CharacterId?.ToString(),
                        tags = gen.Tags ?? Array.Empty<string>()
                    });
                    return await cursor.SingleAsync();
                });
                current = newId;
            }

            return generated;
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