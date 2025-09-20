namespace Npc.Api.Services
{
    public interface ILocalizationService
    {
        // Get localized content for a specific entity and property
        Task<string?> GetLocalizedContentAsync(string entityType, Guid entityId, string propertyName, string languageCode);

        // Set localized content for a specific entity and property
        Task SetLocalizedContentAsync(string entityType, Guid entityId, string propertyName, string languageCode, string content, string? translatedBy = null);

        // Get all localizations for an entity
        Task<Dictionary<string, Dictionary<string, string>>> GetEntityLocalizationsAsync(string entityType, Guid entityId);

        // Get supported languages
        Task<IEnumerable<string>> GetSupportedLanguagesAsync();

        // Get default language
        Task<string> GetDefaultLanguageAsync();

        // Bulk set localized content for multiple properties
        Task SetBulkLocalizedContentAsync(string entityType, Guid entityId, string languageCode, Dictionary<string, string> contentMap, string? translatedBy = null);

        // Delete all localizations for an entity
        Task DeleteEntityLocalizationsAsync(string entityType, Guid entityId);

        // Get translation completion status for an entity
        Task<Dictionary<string, double>> GetTranslationCompletionAsync(string entityType, Guid entityId);

        // Auto-translate content using external services (placeholder for future implementation)
        Task<string> AutoTranslateAsync(string content, string fromLanguage, string toLanguage);

        // Validate if a language code is supported
        Task<bool> IsLanguageSupportedAsync(string languageCode);
    }
}