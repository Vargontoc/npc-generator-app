using Npc.Api.Services;
using System.Globalization;

namespace Npc.Api.Middleware
{
    public class LocalizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LocalizationMiddleware> _logger;

        public LocalizationMiddleware(RequestDelegate next, ILogger<LocalizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
        {
            var requestedLanguage = GetRequestedLanguage(context);

            if (!string.IsNullOrEmpty(requestedLanguage))
            {
                var isSupported = await localizationService.IsLanguageSupportedAsync(requestedLanguage);
                if (isSupported)
                {
                    // Set the language code in the context for controllers to use
                    context.Items["RequestedLanguage"] = requestedLanguage;

                    // Set the culture for this request
                    try
                    {
                        var culture = new CultureInfo(requestedLanguage);
                        CultureInfo.CurrentCulture = culture;
                        CultureInfo.CurrentUICulture = culture;
                    }
                    catch (CultureNotFoundException)
                    {
                        _logger.LogWarning("Invalid culture info for language code: {LanguageCode}", requestedLanguage);
                    }
                }
                else
                {
                    _logger.LogWarning("Unsupported language requested: {LanguageCode}", requestedLanguage);
                }
            }

            await _next(context);
        }

        private static string? GetRequestedLanguage(HttpContext context)
        {
            // Priority order: Query parameter > Header > Accept-Language header

            // 1. Check query parameter
            if (context.Request.Query.TryGetValue("lang", out var langParam))
            {
                var lang = langParam.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(lang))
                    return lang;
            }

            // 2. Check custom header
            if (context.Request.Headers.TryGetValue("X-Language", out var langHeader))
            {
                var lang = langHeader.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(lang))
                    return lang;
            }

            // 3. Check Accept-Language header
            var acceptLanguageHeader = context.Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrEmpty(acceptLanguageHeader))
            {
                // Parse Accept-Language header and get the first preferred language
                var languages = acceptLanguageHeader
                    .Split(',')
                    .Select(lang => lang.Split(';')[0].Trim().ToLowerInvariant())
                    .Where(lang => !string.IsNullOrEmpty(lang))
                    .ToArray();

                return languages.FirstOrDefault();
            }

            return null;
        }
    }

    public static class LocalizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseLocalization(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LocalizationMiddleware>();
        }
    }
}