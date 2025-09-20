using Npc.Api.Entities;
using Npc.Api.Services;

namespace Npc.Api.Extensions
{
    public static class LocalizationExtensions
    {
        public static async Task<T> WithLocalizationAsync<T>(this T entity, ILocalizationService localizationService, string languageCode)
            where T : BaseEntity, ILocalizable
        {
            var localizations = await localizationService.GetEntityLocalizationsAsync(entity.GetEntityType(), entity.Id);

            if (localizations.TryGetValue(languageCode, out var translations))
            {
                entity.ApplyLocalizations(translations);
            }

            return entity;
        }

        public static async Task SaveLocalizationAsync<T>(this T entity, ILocalizationService localizationService, string languageCode, string? translatedBy = null)
            where T : BaseEntity, ILocalizable
        {
            var localizations = entity.GetLocalizableProperties();
            await localizationService.SetBulkLocalizedContentAsync(
                entity.GetEntityType(),
                entity.Id,
                languageCode,
                localizations,
                translatedBy);
        }

        public static async Task<Dictionary<string, double>> GetTranslationStatusAsync<T>(this T entity, ILocalizationService localizationService)
            where T : BaseEntity, ILocalizable
        {
            return await localizationService.GetTranslationCompletionAsync(entity.GetEntityType(), entity.Id);
        }
    }

    public interface ILocalizable
    {
        string GetEntityType();
        Dictionary<string, string> GetLocalizableProperties();
        void ApplyLocalizations(Dictionary<string, string> localizations);
    }
}