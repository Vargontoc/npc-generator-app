namespace Npc.Api.Dtos
{
    public record TtsSynthesizeRequest(string Text, string Voice, string Format = "wav", int? Sample_Rate = null, double? Length_Scale = null, double? Noise_Scale = null, double? Noise_W = null, double? Speaker = null);
    public record ImageRequest(string Prompt, string? Negative_Prompt, int Width = 1024, int Height = 1024, int Steps = 25, double Cfg = 7.5, int? Seed = null, string? Model = null);

    public record ImageJobAccepted(string Job_Id, string Status);
    public record ImageItem(string Image_Id, string Url, int? Seed);
    public record ImageJobStatus(string Status, List<ImageItem> Images, Dictionary<string, string>? Audit, Dictionary<string, string> Error);

    public record AssignAvatarRequest(string JobId);
}