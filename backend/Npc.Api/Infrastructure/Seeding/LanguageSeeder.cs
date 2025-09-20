using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Entities;

namespace Npc.Api.Infrastructure.Seeding
{
    public static class LanguageSeeder
    {
        public static async Task SeedLanguagesAsync(CharacterDbContext context)
        {
            if (await context.Languages.AnyAsync())
                return; // Languages already seeded

            var languages = new List<Language>
            {
                new()
                {
                    Code = "en",
                    Name = "English",
                    NativeName = "English",
                    CultureInfo = "en-US",
                    IsActive = true,
                    IsDefault = true,
                    SortOrder = 1,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "es",
                    Name = "Spanish",
                    NativeName = "Español",
                    CultureInfo = "es-ES",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 2,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "fr",
                    Name = "French",
                    NativeName = "Français",
                    CultureInfo = "fr-FR",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 3,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "de",
                    Name = "German",
                    NativeName = "Deutsch",
                    CultureInfo = "de-DE",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 4,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "pt",
                    Name = "Portuguese",
                    NativeName = "Português",
                    CultureInfo = "pt-BR",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 5,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "it",
                    Name = "Italian",
                    NativeName = "Italiano",
                    CultureInfo = "it-IT",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 6,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "ja",
                    Name = "Japanese",
                    NativeName = "日本語",
                    CultureInfo = "ja-JP",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 7,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "ko",
                    Name = "Korean",
                    NativeName = "한국어",
                    CultureInfo = "ko-KR",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 8,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "zh",
                    Name = "Chinese (Simplified)",
                    NativeName = "简体中文",
                    CultureInfo = "zh-CN",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 9,
                    IsRightToLeft = false
                },
                new()
                {
                    Code = "ar",
                    Name = "Arabic",
                    NativeName = "العربية",
                    CultureInfo = "ar-SA",
                    IsActive = true,
                    IsDefault = false,
                    SortOrder = 10,
                    IsRightToLeft = true
                }
            };

            context.Languages.AddRange(languages);
            await context.SaveChangesAsync();
        }
    }
}