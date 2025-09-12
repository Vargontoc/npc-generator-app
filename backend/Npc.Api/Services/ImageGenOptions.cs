namespace Npc.Api.Services
{
    public class ImageGenOptions
    {
        public string BaseUrl { get; set; } = "";
        public string? ApiKey { get; set; } = "";
        public int Timeout { get; set; } = 15;
    }
}