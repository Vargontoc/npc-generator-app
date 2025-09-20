using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;
using Npc.Api.Infrastructure.Cache;

namespace Npc.Api.Services.Impl
{
    public class LocalizationService : ILocalizationService
    {
        private readonly CharacterDbContext _context;
        private readonly ICacheService _cache;
        private readonly ILogger<LocalizationService> _logger;

        public LocalizationService(CharacterDbContext context, ICacheService cache, ILogger<LocalizationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<string?> GetLocalizedContentAsync(string entityType, Guid entityId, string propertyName, string languageCode)
        {
            var cacheKey = $"localized:{entityType}:{entityId}:{propertyName}:{languageCode}";

            var cached = await _cache.GetAsync<string>(cacheKey);
            if (cached != null)
                return cached;

            var content = await _context.LocalizedContents
                .Where(lc => lc.EntityType == entityType &&
                           lc.EntityId == entityId &&
                           lc.PropertyName == propertyName &&
                           lc.LanguageCode == languageCode)
                .Select(lc => lc.Content)
                .FirstOrDefaultAsync();

            if (content != null)
            {
                await _cache.SetAsync(cacheKey, content, TimeSpan.FromHours(1));
            }

            return content;
        }

        public async Task SetLocalizedContentAsync(string entityType, Guid entityId, string propertyName, string languageCode, string content, string? translatedBy = null)
        {
            var existing = await _context.LocalizedContents
                .FirstOrDefaultAsync(lc => lc.EntityType == entityType &&
                                         lc.EntityId == entityId &&
                                         lc.PropertyName == propertyName &&
                                         lc.LanguageCode == languageCode);

            if (existing != null)
            {
                existing.Content = content;
                existing.TranslatedAt = DateTimeOffset.UtcNow;
                existing.TranslatedBy = translatedBy;
                _context.LocalizedContents.Update(existing);
            }
            else
            {
                var newContent = new LocalizedContent
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    PropertyName = propertyName,
                    LanguageCode = languageCode,
                    Content = content,
                    TranslatedAt = DateTimeOffset.UtcNow,
                    TranslatedBy = translatedBy,
                    IsDefault = await IsDefaultLanguageAsync(languageCode)
                };

                _context.LocalizedContents.Add(newContent);
            }

            await _context.SaveChangesAsync();

            // Invalidate cache
            var cacheKey = $"localized:{entityType}:{entityId}:{propertyName}:{languageCode}";
            await _cache.RemoveAsync(cacheKey);
        }

        public async Task<Dictionary<string, Dictionary<string, string>>> GetEntityLocalizationsAsync(string entityType, Guid entityId)
        {
            var localizations = await _context.LocalizedContents
                .Where(lc => lc.EntityType == entityType && lc.EntityId == entityId)
                .GroupBy(lc => lc.LanguageCode)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.ToDictionary(lc => lc.PropertyName, lc => lc.Content)
                );

            return localizations;
        }

        public async Task<IEnumerable<string>> GetSupportedLanguagesAsync()
        {
            var cacheKey = "supported_languages";
            var cached = await _cache.GetAsync<IEnumerable<string>>(cacheKey);
            if (cached != null)
                return cached;

            var languages = await _context.Languages
                .Where(l => l.IsActive)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .Select(l => l.Code)
                .ToListAsync();

            await _cache.SetAsync(cacheKey, languages, TimeSpan.FromHours(24));
            return languages;
        }

        public async Task<string> GetDefaultLanguageAsync()
        {
            var cacheKey = "default_language";
            var cached = await _cache.GetAsync<string>(cacheKey);
            if (cached != null)
                return cached;

            var defaultLanguage = await _context.Languages
                .Where(l => l.IsDefault && l.IsActive)
                .Select(l => l.Code)
                .FirstOrDefaultAsync();

            defaultLanguage ??= "en"; // Fallback to English

            await _cache.SetAsync(cacheKey, defaultLanguage, TimeSpan.FromHours(24));
            return defaultLanguage;
        }

        public async Task SetBulkLocalizedContentAsync(string entityType, Guid entityId, string languageCode, Dictionary<string, string> contentMap, string? translatedBy = null)
        {
            var existingContents = await _context.LocalizedContents
                .Where(lc => lc.EntityType == entityType &&
                           lc.EntityId == entityId &&
                           lc.LanguageCode == languageCode)
                .ToListAsync();

            var isDefault = await IsDefaultLanguageAsync(languageCode);

            foreach (var (propertyName, content) in contentMap)
            {
                var existing = existingContents.FirstOrDefault(lc => lc.PropertyName == propertyName);

                if (existing != null)
                {
                    existing.Content = content;
                    existing.TranslatedAt = DateTimeOffset.UtcNow;
                    existing.TranslatedBy = translatedBy;
                    _context.LocalizedContents.Update(existing);
                }
                else
                {
                    var newContent = new LocalizedContent
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        PropertyName = propertyName,
                        LanguageCode = languageCode,
                        Content = content,
                        TranslatedAt = DateTimeOffset.UtcNow,
                        TranslatedBy = translatedBy,
                        IsDefault = isDefault
                    };

                    _context.LocalizedContents.Add(newContent);
                }

                // Invalidate cache
                var cacheKey = $"localized:{entityType}:{entityId}:{propertyName}:{languageCode}";
                await _cache.RemoveAsync(cacheKey);
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteEntityLocalizationsAsync(string entityType, Guid entityId)
        {
            var localizations = await _context.LocalizedContents
                .Where(lc => lc.EntityType == entityType && lc.EntityId == entityId)
                .ToListAsync();

            _context.LocalizedContents.RemoveRange(localizations);
            await _context.SaveChangesAsync();

            // Invalidate all related cache entries
            foreach (var localization in localizations)
            {
                var cacheKey = $"localized:{entityType}:{entityId}:{localization.PropertyName}:{localization.LanguageCode}";
                await _cache.RemoveAsync(cacheKey);
            }
        }

        public async Task<Dictionary<string, double>> GetTranslationCompletionAsync(string entityType, Guid entityId)
        {
            var supportedLanguages = await GetSupportedLanguagesAsync();
            var defaultLanguage = await GetDefaultLanguageAsync();

            // Get properties that exist in the default language
            var defaultProperties = await _context.LocalizedContents
                .Where(lc => lc.EntityType == entityType &&
                           lc.EntityId == entityId &&
                           lc.LanguageCode == defaultLanguage)
                .Select(lc => lc.PropertyName)
                .ToListAsync();

            if (!defaultProperties.Any())
                return new Dictionary<string, double>();

            var completion = new Dictionary<string, double>();

            foreach (var language in supportedLanguages)
            {
                if (language == defaultLanguage)
                {
                    completion[language] = 100.0; // Default language is always 100% complete
                    continue;
                }

                var translatedProperties = await _context.LocalizedContents
                    .Where(lc => lc.EntityType == entityType &&
                               lc.EntityId == entityId &&
                               lc.LanguageCode == language &&
                               defaultProperties.Contains(lc.PropertyName))
                    .CountAsync();

                var completionPercentage = defaultProperties.Count > 0
                    ? (double)translatedProperties / defaultProperties.Count * 100.0
                    : 0.0;

                completion[language] = Math.Round(completionPercentage, 2);
            }

            return completion;
        }

        public Task<string> AutoTranslateAsync(string content, string fromLanguage, string toLanguage)
        {
            // Placeholder for future implementation with external translation services
            // Could integrate with Azure Translator, Google Translate, etc.
            _logger.LogWarning("Auto-translation not implemented yet. Returning original content.");
            return Task.FromResult(content);
        }

        public async Task<bool> IsLanguageSupportedAsync(string languageCode)
        {
            var supportedLanguages = await GetSupportedLanguagesAsync();
            return supportedLanguages.Contains(languageCode);
        }

        private async Task<bool> IsDefaultLanguageAsync(string languageCode)
        {
            var defaultLanguage = await GetDefaultLanguageAsync();
            return languageCode == defaultLanguage;
        }
    }
}