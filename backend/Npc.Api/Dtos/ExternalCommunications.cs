namespace Npc.Api.Dtos
{
    public record TtsSynthesizeRequest(string Text, string Voice, string Format = "wav", int? Sample_Rate = null, double? Length_Scale = null, double? Noise_Scale = null, double? Noise_W = null, double? Speaker = null);
}