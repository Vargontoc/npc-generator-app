using Neo4j.Driver;
using Npc.Api.Dtos;

namespace Npc.Api.Services.Impl
{
    public class ConversationGraphService(IDriver driver) : IConversationGraphService
    {
        public async Task AddBranchAsync(Guid fromUtteranceId, Guid toUtteranceId, CancellationToken ct)
        {
            var cypher = @"
                MATCH (a:Utterance {id:$fromId}), (b:Utterance {id:toId})
                MERGE (a)-[:BRANCH_TO]->(b)
            ";
            await using var session = driver.AsyncSession();
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
                    MATCH (u1:Utterance {id:$fromId})
                    CREATE (u2:Utterance {id:$id, text:$text, characterId:$charId, createdAt:datetime()})
                    CREATE (u1)-[:NEXT]->(u2)
                    RETURN u2.id AS id, u2.text AS text, u2.characterId AS characterId
                    """;
            return await RunUtteranceByFrom(cypher, fromUtteranceId, id, text, characterId);
        }



        public async Task<UtteranceResponse> AddRootUtteranceAsync(Guid conversationId, string text, Guid? characterId, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var cypher = """
                MATCH (c:Conversation {id:$cid})
                MERGE (c)-[:ROOT]->(u:Utterance {id:$id})
                SET u.text=$text,
                    u.characterId=$charId,
                    u.createdAt=coalesce(u.createdAt, datetime())
                RETURN u.id AS id, u.text AS text, u.characterId AS characterId
                """;
            return await RunUtterance(cypher, conversationId, id, text, characterId);
        }


        public async Task<ConversationResponse> CreateConversationAsync(string title, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var cypher = """
                CREATE (c:Conversation {id:$id, title:$title, createdAt:datetime()})
                RETURN c.id AS id, c.title AS title
                """;
            await using var session = driver.AsyncSession();
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
                MATCH (c:Conversation {id:$cid})-[:ROOT]->(u:Utterance)
                OPTIONAL MATCH p=(u)-[:NEXT*0..25]->(last)
                WITH c, nodes(p) AS ns
                RETURN c.id AS cid, c.title AS title, [n IN ns | { id:n.id, text:n.text, characterId:n.characterId }] AS seq
                LIMIT 1
                """;
            await using var session = driver.AsyncSession();
            var rec = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { cid = conversationId.ToString() });
                return await cursor.SingleAsync();
            });
            if (rec is null) return null;

            var arr = rec["seq"].As<List<object>>().Select(o =>
            {
                var m = (IDictionary<string, object>)o;
                Guid.TryParse(m["id"]?.ToString(), out var uid);
                Guid? charId = Guid.TryParse(m["characterId"]?.ToString(), out var cc) ? cc : null;
                return new UtteranceResponse(uid, m["text"]?.ToString() ?? "", charId);
            }).ToArray();

            return new PathResponse(Guid.Parse(rec["cid"].As<string>()), rec["title"].As<string>(), arr);
        }
        
        private async Task<UtteranceResponse> RunUtterance(string cypher, Guid conversationId, Guid id, string text, Guid? characterId)
        {
            await using var session = driver.AsyncSession();
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
            Guid.TryParse(rec["id"].As<string>(), out var uid);
            Guid? charId = Guid.TryParse(rec["characterId"]?.As<string>(), out var cc) ? cc : null;
            return new UtteranceResponse(uid, rec["text"].As<string>(), charId);
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
    }
}