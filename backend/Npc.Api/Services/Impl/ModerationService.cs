
namespace Npc.Api.Services.Impl
{
    public class ModerationService(IModerationAgent? agent) : IModerationService
    {
        public async Task<ModerationAdvisory> AnalyzeAsync(int age, string? description, CancellationToken ct)
        {
            if (agent is null || string.IsNullOrWhiteSpace(description))
                return new(false, [], null);

            return await agent.ClassifyAsync(age, description, ct);
        }
    }
}