namespace Npc.Api.Services
{
    public class TtsOptions
    {
        public string BaseUrl { get; set; } = "";
        public string? ApiKey { get; set; } = "";
        public int Timeout { get; set; } = 15;
    }
}