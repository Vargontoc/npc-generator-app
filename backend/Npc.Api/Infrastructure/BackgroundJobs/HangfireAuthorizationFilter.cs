using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Npc.Api.Infrastructure.BackgroundJobs
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            // In development, allow all access
            // In production, you should implement proper authorization
            var httpContext = context.GetHttpContext();
            return httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        }
    }
}