namespace Npc.Api.Services
{
    public record ModerationAdvisory(bool HasAdvisory, string[] Flags, string? Message);

    public interface IModerationService
    {
        Task<ModerationAdvisory> AnalyzeAsync(int age, string? description, CancellationToken ct);
    }

    public interface IModerationAgent
    {
        Task<ModerationAdvisory> ClassifyAsync(int age, string? description, CancellationToken ct);
    }
}