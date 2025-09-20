using Microsoft.AspNetCore.Mvc;
using Npc.Api.Services;
using Npc.Api.Entities;
using Npc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocalizationController : ControllerBase
    {
        private readonly ILocalizationService _localizationService;
        private readonly CharacterDbContext _context;
        private readonly ILogger<LocalizationController> _logger;

        public LocalizationController(
            ILocalizationService localizationService,
            CharacterDbContext context,
            ILogger<LocalizationController> logger)
        {
            _localizationService = localizationService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("languages")]
        public async Task<ActionResult<IEnumerable<LanguageResponse>>> GetSupportedLanguages()
        {
            var languages = await _context.Languages
                .Where(l => l.IsActive)
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Name)
                .Select(l => new LanguageResponse(l.Code, l.Name, l.NativeName, l.IsDefault, l.IsRightToLeft))
                .ToListAsync();

            return Ok(languages);
        }

        [HttpPost("languages")]
        public async Task<ActionResult<LanguageResponse>> CreateLanguage([FromBody] CreateLanguageRequest request)
        {
            var existingLanguage = await _context.Languages
                .FirstOrDefaultAsync(l => l.Code == request.Code);

            if (existingLanguage != null)
                return Conflict($"Language with code '{request.Code}' already exists.");

            var language = new Language
            {
                Code = request.Code,
                Name = request.Name,
                NativeName = request.NativeName,
                CultureInfo = request.CultureInfo,
                IsRightToLeft = request.IsRightToLeft,
                IsActive = true,
                SortOrder = request.SortOrder
            };

            _context.Languages.Add(language);
            await _context.SaveChangesAsync();

            var response = new LanguageResponse(language.Code, language.Name, language.NativeName, language.IsDefault, language.IsRightToLeft);
            return CreatedAtAction(nameof(GetSupportedLanguages), new { code = language.Code }, response);
        }

        [HttpGet("{entityType}/{entityId}/localizations")]
        public async Task<ActionResult<Dictionary<string, Dictionary<string, string>>>> GetEntityLocalizations(
            string entityType, Guid entityId)
        {
            var localizations = await _localizationService.GetEntityLocalizationsAsync(entityType, entityId);
            return Ok(localizations);
        }

        [HttpPost("{entityType}/{entityId}/localizations/{languageCode}")]
        public async Task<IActionResult> SetEntityLocalizations(
            string entityType, Guid entityId, string languageCode,
            [FromBody] Dictionary<string, string> contentMap)
        {
            if (!await _localizationService.IsLanguageSupportedAsync(languageCode))
                return BadRequest($"Language '{languageCode}' is not supported.");

            await _localizationService.SetBulkLocalizedContentAsync(entityType, entityId, languageCode, contentMap);
            return NoContent();
        }

        [HttpGet("{entityType}/{entityId}/translation-status")]
        public async Task<ActionResult<Dictionary<string, double>>> GetTranslationStatus(string entityType, Guid entityId)
        {
            var status = await _localizationService.GetTranslationCompletionAsync(entityType, entityId);
            return Ok(status);
        }

        [HttpPost("{entityType}/{entityId}/auto-translate")]
        public async Task<IActionResult> AutoTranslateEntity(
            string entityType, Guid entityId,
            [FromBody] AutoTranslateRequest request)
        {
            if (!await _localizationService.IsLanguageSupportedAsync(request.FromLanguage))
                return BadRequest($"Source language '{request.FromLanguage}' is not supported.");

            if (!await _localizationService.IsLanguageSupportedAsync(request.ToLanguage))
                return BadRequest($"Target language '{request.ToLanguage}' is not supported.");

            // Get source content
            var sourceLocalizations = await _localizationService.GetEntityLocalizationsAsync(entityType, entityId);
            if (!sourceLocalizations.TryGetValue(request.FromLanguage, out var sourceContent))
                return NotFound($"No content found for entity in language '{request.FromLanguage}'.");

            // Auto-translate content (placeholder implementation)
            var translatedContent = new Dictionary<string, string>();
            foreach (var (property, content) in sourceContent)
            {
                var translatedText = await _localizationService.AutoTranslateAsync(content, request.FromLanguage, request.ToLanguage);
                translatedContent[property] = translatedText;
            }

            // Save translated content
            await _localizationService.SetBulkLocalizedContentAsync(entityType, entityId, request.ToLanguage, translatedContent, "auto-translate");

            return NoContent();
        }

        [HttpDelete("{entityType}/{entityId}/localizations")]
        public async Task<IActionResult> DeleteEntityLocalizations(string entityType, Guid entityId)
        {
            await _localizationService.DeleteEntityLocalizationsAsync(entityType, entityId);
            return NoContent();
        }
    }

    // DTOs for the localization API
    public record LanguageResponse(string Code, string Name, string NativeName, bool IsDefault, bool IsRightToLeft);
    public record CreateLanguageRequest(string Code, string Name, string NativeName, string? CultureInfo, bool IsRightToLeft, int SortOrder);
    public record AutoTranslateRequest(string FromLanguage, string ToLanguage);
}