
namespace Npc.Api.Services.Impl
{
    public class ModerationAgentService : IModerationAgent
    {
        public Task<ModerationAdvisory> ClassifyAsync(int age, string? description, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(description))
                return Task.FromResult(new ModerationAdvisory(false, [], null));
            return Task.FromResult(new ModerationAdvisory(true, [], "Potential sensitive content detected"));
        }
    }
}