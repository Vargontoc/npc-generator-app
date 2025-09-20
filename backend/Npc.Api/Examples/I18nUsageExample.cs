using Npc.Api.Entities;
using Npc.Api.Services;
using Npc.Api.Extensions;

namespace Npc.Api.Examples
{
    /// <summary>
    /// Example demonstrating how to use the internationalization (i18n) system
    /// for NPCs, dialogues, and lore content.
    /// </summary>
    public class I18nUsageExample
    {
        private readonly ILocalizationService _localizationService;

        public I18nUsageExample(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        /// <summary>
        /// Example: Creating a multilingual NPC character
        /// </summary>
        public async Task CreateMultilingualCharacterExample()
        {
            // 1. Create character with default language (English)
            var character = new Character
            {
                Name = "Sir Gareth the Bold",
                Age = 35,
                Description = "A brave knight who protects the realm from evil forces."
            };

            // 2. Add Spanish translations
            await character.SaveLocalizationAsync(_localizationService, "es");
            await _localizationService.SetBulkLocalizedContentAsync(
                character.GetEntityType(),
                character.Id,
                "es",
                new Dictionary<string, string>
                {
                    { "Name", "Sir Gareth el Valiente" },
                    { "Description", "Un caballero valiente que protege el reino de las fuerzas del mal." }
                },
                "manual-translation");

            // 3. Add French translations
            await _localizationService.SetBulkLocalizedContentAsync(
                character.GetEntityType(),
                character.Id,
                "fr",
                new Dictionary<string, string>
                {
                    { "Name", "Sir Gareth le Brave" },
                    { "Description", "Un chevalier courageux qui protège le royaume des forces du mal." }
                },
                "manual-translation");

            // 4. Retrieve character in different languages
            var characterInSpanish = await character.WithLocalizationAsync(_localizationService, "es");
            var characterInFrench = await character.WithLocalizationAsync(_localizationService, "fr");

            Console.WriteLine($"English: {character.Name} - {character.Description}");
            Console.WriteLine($"Spanish: {characterInSpanish.Name} - {characterInSpanish.Description}");
            Console.WriteLine($"French: {characterInFrench.Name} - {characterInFrench.Description}");
        }

        /// <summary>
        /// Example: Creating multilingual lore content
        /// </summary>
        public async Task CreateMultilingualLoreExample()
        {
            var lore = new Lore
            {
                Title = "The Legend of the Dragon's Heart",
                Text = "Long ago, in the mystical kingdom of Aethermoor, there existed a powerful artifact known as the Dragon's Heart. This gem held the power to control dragons and was sought after by many heroes and villains alike."
            };

            // Add German translation
            await _localizationService.SetBulkLocalizedContentAsync(
                lore.GetEntityType(),
                lore.Id,
                "de",
                new Dictionary<string, string>
                {
                    { "Title", "Die Legende des Drachenherzes" },
                    { "Text", "Vor langer Zeit, im mystischen Königreich Aethermoor, existierte ein mächtiges Artefakt namens das Drachenherz. Dieser Edelstein besaß die Macht, Drachen zu kontrollieren und wurde von vielen Helden und Schurken gleichermaßen begehrt." }
                },
                "auto-translate");

            // Add Japanese translation
            await _localizationService.SetBulkLocalizedContentAsync(
                lore.GetEntityType(),
                lore.Id,
                "ja",
                new Dictionary<string, string>
                {
                    { "Title", "ドラゴンハートの伝説" },
                    { "Text", "昔々、神秘的なエーテルムーア王国に、ドラゴンハートとして知られる強力なアーティファクトが存在していました。この宝石はドラゴンを制御する力を持ち、多くの英雄と悪役に同じように求められていました。" }
                },
                "professional-translation");
        }

        /// <summary>
        /// Example: Creating multilingual dialogue/conversation
        /// </summary>
        public async Task CreateMultilingualDialogueExample()
        {
            var conversation = new Conversation
            {
                Title = "Meeting the Village Elder"
            };

            var utterance1 = new Utterance
            {
                Text = "Welcome, traveler. What brings you to our humble village?",
                ConversationId = conversation.Id
            };

            var utterance2 = new Utterance
            {
                Text = "I seek the ancient tome of wisdom. Can you help me?",
                ConversationId = conversation.Id
            };

            // Add Spanish translations for the conversation
            await conversation.SaveLocalizationAsync(_localizationService, "es");
            await _localizationService.SetLocalizedContentAsync(
                conversation.GetEntityType(),
                conversation.Id,
                "Title",
                "es",
                "Encuentro con el Anciano del Pueblo");

            // Add Spanish translations for utterances
            await _localizationService.SetLocalizedContentAsync(
                utterance1.GetEntityType(),
                utterance1.Id,
                "Text",
                "es",
                "Bienvenido, viajero. ¿Qué te trae a nuestro humilde pueblo?");

            await _localizationService.SetLocalizedContentAsync(
                utterance2.GetEntityType(),
                utterance2.Id,
                "Text",
                "es",
                "Busco el antiguo tomo de la sabiduría. ¿Puedes ayudarme?");
        }

        /// <summary>
        /// Example: Checking translation completion status
        /// </summary>
        public async Task CheckTranslationStatusExample(Character character)
        {
            var translationStatus = await character.GetTranslationStatusAsync(_localizationService);

            Console.WriteLine("Translation Completion Status:");
            foreach (var (language, completion) in translationStatus)
            {
                Console.WriteLine($"{language}: {completion:F1}%");
            }
        }

        /// <summary>
        /// Example: Auto-translation workflow (placeholder for future implementation)
        /// </summary>
        public async Task AutoTranslationWorkflowExample(Character character)
        {
            // Get all supported languages
            var supportedLanguages = await _localizationService.GetSupportedLanguagesAsync();
            var defaultLanguage = await _localizationService.GetDefaultLanguageAsync();

            foreach (var targetLanguage in supportedLanguages)
            {
                if (targetLanguage == defaultLanguage)
                    continue;

                try
                {
                    // Get default language content
                    var defaultLocalizations = await _localizationService.GetEntityLocalizationsAsync(
                        character.GetEntityType(), character.Id);

                    if (defaultLocalizations.TryGetValue(defaultLanguage, out var defaultContent))
                    {
                        var translatedContent = new Dictionary<string, string>();

                        foreach (var (property, content) in defaultContent)
                        {
                            var translatedText = await _localizationService.AutoTranslateAsync(
                                content, defaultLanguage, targetLanguage);
                            translatedContent[property] = translatedText;
                        }

                        await _localizationService.SetBulkLocalizedContentAsync(
                            character.GetEntityType(),
                            character.Id,
                            targetLanguage,
                            translatedContent,
                            "auto-translate");

                        Console.WriteLine($"Auto-translated character to {targetLanguage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to auto-translate to {targetLanguage}: {ex.Message}");
                }
            }
        }
    }
}