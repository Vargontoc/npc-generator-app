namespace Npc.Api.Services
{
    public class AgentOptions
    {
        public string BaseUrl { get; set; } = "";
        public string? ApiKey { get; set; } = "";
        public int Timeout { get; set; } = 15;
    }
}