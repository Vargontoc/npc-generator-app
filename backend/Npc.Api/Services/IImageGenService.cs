using Npc.Api.Dtos;

namespace Npc.Api.Services
{
    public interface IImageGenService
    {
        Task<string> GetModelsRawAsync(CancellationToken ct);
        Task<ImageJobAccepted> GenerateAsync(ImageRequest req, CancellationToken ct);
        Task<ImageJobStatus> GetJobAsync(string jobId, CancellationToken ct);
    }
}