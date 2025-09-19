using Npc.Api.Dtos;

namespace Npc.Api.Services 
{
    public interface ITtsService
    {
        Task<string> GetVoices(CancellationToken ct);
        Task<(Stream Stream, string ContentType, string? FileName)> GenerateVoice(TtsSynthesizeRequest req, CancellationToken ct);
    }
}