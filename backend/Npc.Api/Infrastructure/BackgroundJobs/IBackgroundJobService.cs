namespace Npc.Api.Infrastructure.BackgroundJobs
{
    public interface IBackgroundJobService
    {
        string EnqueueImageGeneration(Guid characterId, string prompt);
        string EnqueueBulkCharacterGeneration(Guid worldId, int count);
        string EnqueueLoreGeneration(Guid worldId, string theme);
        string EnqueueConversationCleanup();
        string ScheduleWorldStatisticsUpdate(Guid worldId, TimeSpan delay);
        void DeleteJob(string jobId);
    }
}