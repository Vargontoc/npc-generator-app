using Neo4j.Driver;

namespace Npc.Api.Services
{
    public static class Neo4jBootstrap {
        private static readonly string[] Cypher = [
            "CREATE CONSTRAINT conv_id IF NOT EXISTS FOR (c:Conversation) REQUIRE c.id IS UNIQUE",
            "CREATE CONSTRAINT utt_id IF NOT EXISTS FOR (u:Utterance) REQUIRE u.id IS UNIQUE",
            "CREATE INDEX utt_character IF NOT EXISTS FOR (u:Utterance) ON (u.characterId)"
        ];

        public static async Task EnsureAsync(IDriver driver, CancellationToken ct)
        {
            await using var session = driver.AsyncSession(o => o.WithDefaultAccessMode(AccessMode.Write));
            foreach (var c in Cypher)
                await session.ExecuteWriteAsync(tx => tx.RunAsync(c));
        }

    }

}