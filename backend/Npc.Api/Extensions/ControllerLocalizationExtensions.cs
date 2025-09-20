using Microsoft.AspNetCore.Mvc;
using Npc.Api.Entities;
using Npc.Api.Services;

namespace Npc.Api.Extensions
{
    public static class ControllerLocalizationExtensions
    {
        public static string GetRequestedLanguage(this ControllerBase controller)
        {
            if (controller.HttpContext.Items.TryGetValue("RequestedLanguage", out var language) && language is string lang)
                return lang;

            return "en"; // Default fallback
        }

        public static async Task<T> LocalizeAsync<T>(this T entity, ControllerBase controller, ILocalizationService localizationService)
            where T : BaseEntity, ILocalizable
        {
            var requestedLanguage = controller.GetRequestedLanguage();
            var defaultLanguage = await localizationService.GetDefaultLanguageAsync();

            // If requesting default language, no need to apply localizations
            if (requestedLanguage == defaultLanguage)
                return entity;

            return await entity.WithLocalizationAsync(localizationService, requestedLanguage);
        }

        public static async Task<IEnumerable<T>> LocalizeAsync<T>(this IEnumerable<T> entities, ControllerBase controller, ILocalizationService localizationService)
            where T : BaseEntity, ILocalizable
        {
            var requestedLanguage = controller.GetRequestedLanguage();
            var defaultLanguage = await localizationService.GetDefaultLanguageAsync();

            // If requesting default language, no need to apply localizations
            if (requestedLanguage == defaultLanguage)
                return entities;

            var localizedEntities = new List<T>();
            foreach (var entity in entities)
            {
                var localized = await entity.WithLocalizationAsync(localizationService, requestedLanguage);
                localizedEntities.Add(localized);
            }

            return localizedEntities;
        }
    }
}