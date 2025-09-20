using Hangfire;

namespace Npc.Api.Infrastructure.BackgroundJobs
{
    public class HangfireBackgroundJobService : IBackgroundJobService
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<HangfireBackgroundJobService> _logger;

        public HangfireBackgroundJobService(
            IBackgroundJobClient backgroundJobClient,
            ILogger<HangfireBackgroundJobService> logger)
        {
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        public string EnqueueImageGeneration(Guid characterId, string prompt)
        {
            var jobId = _backgroundJobClient.Enqueue<BackgroundJobs>(
                jobs => jobs.GenerateCharacterImageAsync(characterId, prompt, CancellationToken.None));

            _logger.LogInformation("Enqueued image generation job {JobId} for character {CharacterId}",
                jobId, characterId);

            return jobId;
        }

        public string EnqueueBulkCharacterGeneration(Guid worldId, int count)
        {
            var jobId = _backgroundJobClient.Enqueue<BackgroundJobs>(
                jobs => jobs.BulkGenerateCharactersAsync(worldId, count, CancellationToken.None));

            _logger.LogInformation("Enqueued bulk character generation job {JobId} for world {WorldId} (count: {Count})",
                jobId, worldId, count);

            return jobId;
        }

        public string EnqueueLoreGeneration(Guid worldId, string theme)
        {
            var jobId = _backgroundJobClient.Enqueue<BackgroundJobs>(
                jobs => jobs.GenerateWorldLoreAsync(worldId, theme, CancellationToken.None));

            _logger.LogInformation("Enqueued lore generation job {JobId} for world {WorldId} with theme '{Theme}'",
                jobId, worldId, theme);

            return jobId;
        }

        public string EnqueueConversationCleanup()
        {
            var jobId = _backgroundJobClient.Enqueue<BackgroundJobs>(
                jobs => jobs.CleanupOldConversationsAsync(CancellationToken.None));

            _logger.LogInformation("Enqueued conversation cleanup job {JobId}", jobId);

            return jobId;
        }

        public string ScheduleWorldStatisticsUpdate(Guid worldId, TimeSpan delay)
        {
            var jobId = _backgroundJobClient.Schedule<BackgroundJobs>(
                jobs => jobs.UpdateWorldStatisticsAsync(worldId, CancellationToken.None),
                delay);

            _logger.LogInformation("Scheduled world statistics update job {JobId} for world {WorldId} with delay {Delay}",
                jobId, worldId, delay);

            return jobId;
        }

        public void DeleteJob(string jobId)
        {
            _backgroundJobClient.Delete(jobId);
            _logger.LogInformation("Deleted background job {JobId}", jobId);
        }
    }
}