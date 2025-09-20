using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;

namespace Npc.Api.Domain.Events.Handlers
{
    // Enhanced conversation sync handlers for bidirectional sync
    public class ConversationMetadataHandler : IDomainEventHandler<ConversationCreatedEvent>
    {
        private readonly CharacterDbContext _context;
        private readonly ILogger<ConversationMetadataHandler> _logger;

        public ConversationMetadataHandler(CharacterDbContext context, ILogger<ConversationMetadataHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task HandleAsync(ConversationCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                // Create a simple metadata table for conversations if needed
                // This could be used for analytics, reporting, search, etc.
                var sql = """
                    INSERT INTO conversation_metadata (id, title, created_at, total_utterances, last_activity)
                    VALUES (@Id, @Title, @CreatedAt, 0, @CreatedAt)
                    ON CONFLICT (id) DO NOTHING
                    """;

                await _context.Database.ExecuteSqlRawAsync(sql,
                    new Npgsql.NpgsqlParameter("@Id", domainEvent.ConversationId),
                    new Npgsql.NpgsqlParameter("@Title", domainEvent.Title),
                    new Npgsql.NpgsqlParameter("@CreatedAt", DateTimeOffset.UtcNow));

                _logger.LogDebug("Synced conversation metadata to PostgreSQL: {ConversationId}", domainEvent.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync conversation metadata to PostgreSQL: {ConversationId}", domainEvent.ConversationId);
                // Don't throw - this is metadata sync, not critical
            }
        }
    }

    public class UtteranceMetadataHandler : IDomainEventHandler<UtteranceCreatedEvent>
    {
        private readonly CharacterDbContext _context;
        private readonly ILogger<UtteranceMetadataHandler> _logger;

        public UtteranceMetadataHandler(CharacterDbContext context, ILogger<UtteranceMetadataHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task HandleAsync(UtteranceCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                // Update conversation metadata when utterances are added
                var sql = """
                    UPDATE conversation_metadata
                    SET total_utterances = total_utterances + 1,
                        last_activity = @LastActivity
                    WHERE id = @ConversationId
                    """;

                await _context.Database.ExecuteSqlRawAsync(sql,
                    new Npgsql.NpgsqlParameter("@ConversationId", domainEvent.ConversationId),
                    new Npgsql.NpgsqlParameter("@LastActivity", DateTimeOffset.UtcNow));

                // Track character participation for analytics
                if (domainEvent.CharacterId is not null)
                {
                    var participationSql = """
                        INSERT INTO character_conversation_participation (character_id, conversation_id, utterance_count, last_participated)
                        VALUES (@CharacterId, @ConversationId, 1, @LastParticipated)
                        ON CONFLICT (character_id, conversation_id)
                        DO UPDATE SET
                            utterance_count = character_conversation_participation.utterance_count + 1,
                            last_participated = @LastParticipated
                        """;

                    await _context.Database.ExecuteSqlRawAsync(participationSql,
                        new Npgsql.NpgsqlParameter("@CharacterId", domainEvent.CharacterId.Value),
                        new Npgsql.NpgsqlParameter("@ConversationId", domainEvent.ConversationId),
                        new Npgsql.NpgsqlParameter("@LastParticipated", DateTimeOffset.UtcNow));
                }

                _logger.LogDebug("Updated conversation metadata for utterance: {UtteranceId}", domainEvent.UtteranceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update conversation metadata for utterance: {UtteranceId}", domainEvent.UtteranceId);
            }
        }
    }

    // Character relationship inference handler
    public class CharacterRelationshipInferenceHandler : IDomainEventHandler<UtteranceCreatedEvent>
    {
        private readonly Neo4j.Driver.IDriver _neo4jDriver;
        private readonly ILogger<CharacterRelationshipInferenceHandler> _logger;

        public CharacterRelationshipInferenceHandler(Neo4j.Driver.IDriver neo4jDriver, ILogger<CharacterRelationshipInferenceHandler> logger)
        {
            _neo4jDriver = neo4jDriver;
            _logger = logger;
        }

        public async Task HandleAsync(UtteranceCreatedEvent domainEvent, CancellationToken ct = default)
        {
            try
            {
                if (domainEvent.CharacterId is null) return;

                // Infer relationships based on conversation patterns
                const string cypher = """
                    // Find other characters in the same conversation
                    MATCH (conv:Conversation {id: $conversationId})-[:ROOT]->(root)-[:NEXT*0..]->(u:Utterance)
                    WHERE u.characterId IS NOT NULL AND u.characterId <> $currentCharacterId
                    WITH DISTINCT u.characterId AS otherCharacterId

                    // Match current character and other character nodes
                    MATCH (current:Character {id: $currentCharacterId})
                    MATCH (other:Character {id: otherCharacterId})

                    // Create or strengthen INTERACTED_WITH relationship
                    MERGE (current)-[rel:INTERACTED_WITH]->(other)
                    ON CREATE SET rel.strength = 1, rel.firstInteraction = datetime(), rel.conversationCount = 1
                    ON MATCH SET rel.strength = rel.strength + 1, rel.lastInteraction = datetime()

                    // Create bidirectional relationship
                    MERGE (other)-[rel2:INTERACTED_WITH]->(current)
                    ON CREATE SET rel2.strength = 1, rel2.firstInteraction = datetime(), rel2.conversationCount = 1
                    ON MATCH SET rel2.strength = rel2.strength + 1, rel2.lastInteraction = datetime()
                    """;

                await using var session = _neo4jDriver.AsyncSession(o => o.WithDefaultAccessMode(Neo4j.Driver.AccessMode.Write));
                await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
                {
                    conversationId = domainEvent.ConversationId.ToString(),
                    currentCharacterId = domainEvent.CharacterId.Value.ToString()
                }));

                _logger.LogDebug("Inferred character relationships for utterance: {UtteranceId}", domainEvent.UtteranceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to infer character relationships for utterance: {UtteranceId}", domainEvent.UtteranceId);
            }
        }
    }
}