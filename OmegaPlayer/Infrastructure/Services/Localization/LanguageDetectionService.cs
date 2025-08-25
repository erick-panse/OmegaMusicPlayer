using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace OmegaPlayer.Infrastructure.Services
{
    /// <summary>
    /// Service for dynamically detecting and managing available languages
    /// based on JSON files in the Resources/Localization folder
    /// </summary>
    public class LanguageDetectionService
    {
        private const string RESOURCES_PATH = "Resources.Localization";
        private const string DEFAULT_LANGUAGE = "en-US";

        private List<LanguageInfo> _availableLanguages;
        private readonly Dictionary<string, LanguageInfo> _languageCache = new();

        public LanguageDetectionService()
        {
            LoadAvailableLanguages();
        }

        /// <summary>
        /// Gets all available languages from the resources folder
        /// </summary>
        public List<LanguageInfo> GetAvailableLanguages()
        {
            if (_availableLanguages == null || !_availableLanguages.Any())
            {
                LoadAvailableLanguages();
            }

            return _availableLanguages ?? new List<LanguageInfo>();
        }

        /// <summary>
        /// Gets language info by language code
        /// </summary>
        public LanguageInfo GetLanguageInfo(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                languageCode = DEFAULT_LANGUAGE;

            if (_languageCache.TryGetValue(languageCode, out var cachedInfo))
                return cachedInfo;

            return GetAvailableLanguages().FirstOrDefault(l => l.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
                   ?? GetDefaultLanguage();
        }

        /// <summary>
        /// Gets the default language (en-US or first available)
        /// </summary>
        public LanguageInfo GetDefaultLanguage()
        {
            var languages = GetAvailableLanguages();
            return languages.FirstOrDefault(l => l.IsDefault)
                   ?? languages.FirstOrDefault(l => l.LanguageCode == DEFAULT_LANGUAGE)
                   ?? languages.FirstOrDefault()
                   ?? CreateFallbackLanguage();
        }

        /// <summary>
        /// Validates if a language code exists
        /// </summary>
        public bool IsLanguageAvailable(string languageCode)
        {
            return GetAvailableLanguages().Any(l => l.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Loads translations for a specific language
        /// </summary>
        public Dictionary<string, string> LoadLanguageTranslations(string languageCode)
        {

            var assembly = Assembly.GetExecutingAssembly();
            var resourcePath = $"{assembly.GetName().Name}.{RESOURCES_PATH}.{languageCode}.json";

            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var fullData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (fullData != null)
                {
                    // Extract only the translation keys (excluding _metadata)
                    var translations = new Dictionary<string, string>();
                    foreach (var kvp in fullData)
                    {
                        if (kvp.Key != "_metadata" && kvp.Value != null)
                        {
                            translations[kvp.Key] = kvp.Value.ToString();
                        }
                    }
                    return translations;
                }
            }

            return new Dictionary<string, string>();
        }

        private void LoadAvailableLanguages()
        {

            var languages = new List<LanguageInfo>();
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName().Name;

            // Get all embedded resources that match our localization pattern
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith($"{assemblyName}.{RESOURCES_PATH}.") && name.EndsWith(".json"))
                .ToList();

            foreach (var resourceName in resourceNames)
            {
                try
                {
                    // Extract language code from resource name
                    var fileName = resourceName.Substring($"{assemblyName}.{RESOURCES_PATH}.".Length);
                    var languageCode = Path.GetFileNameWithoutExtension(fileName);

                    // Load and parse the JSON file
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var json = reader.ReadToEnd();
                        var fullData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                        var languageInfo = ExtractLanguageInfo(languageCode, fullData);
                        if (languageInfo != null)
                        {
                            languages.Add(languageInfo);
                            _languageCache[languageCode] = languageInfo;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // DO nothing
                }
            }

            // Ensure we have at least a default language
            if (!languages.Any())
            {
                languages.Add(CreateFallbackLanguage());
            }

            // Sort languages: default first, then alphabetically
            _availableLanguages = languages.OrderBy(l => l.IsDefault ? 0 : 1)
                               .ThenBy(l => l.DisplayName)
                               .ToList();

        }

        private LanguageInfo ExtractLanguageInfo(string languageCode, Dictionary<string, object> jsonData)
        {
            try
            {
                // Try to extract metadata
                if (jsonData.TryGetValue("_metadata", out var metadataObj) && metadataObj is JsonElement metadataElement)
                {
                    var metadata = JsonSerializer.Deserialize<LanguageMetadata>(metadataElement.GetRawText());
                    if (metadata != null)
                    {
                        return new LanguageInfo
                        {
                            LanguageCode = metadata.LanguageCode ?? languageCode,
                            DisplayName = metadata.DisplayName ?? GenerateDisplayName(languageCode),
                            NativeName = metadata.NativeName ?? metadata.DisplayName ?? GenerateDisplayName(languageCode),
                            IsDefault = metadata.IsDefault
                        };
                    }
                }

                // Fallback: create language info from filename and common patterns
                return new LanguageInfo
                {
                    LanguageCode = languageCode,
                    DisplayName = GenerateDisplayName(languageCode),
                    NativeName = GenerateDisplayName(languageCode),
                    IsDefault = languageCode.Equals(DEFAULT_LANGUAGE, StringComparison.OrdinalIgnoreCase)
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private string GenerateDisplayName(string languageCode)
        {
            // Generate human-readable display names for common language codes
            return languageCode.ToLowerInvariant() switch
            {
                "en" or "en-us" => "English (United States)",
                "en-gb" => "English (United Kingdom)",
                "es" or "es-es" => "Español (España)",
                "es-mx" => "Español (México)",
                "fr" or "fr-fr" => "Français (France)",
                "fr-ca" => "Français (Canada)",
                "de" or "de-de" => "Deutsch (Deutschland)",
                "pt-br" => "Português (Brasil)",
                "pt" or "pt-pt" => "Português (Portugal)",
                "ja" or "ja-jp" => "日本語 (日本)",
                "zh-cn" => "中文 (简体)",
                "zh-tw" => "中文 (繁體)",
                "it" or "it-it" => "Italiano (Italia)",
                "ru" or "ru-ru" => "Русский (Россия)",
                "ko" or "ko-kr" => "한국어 (대한민국)",
                "ar" or "ar-sa" => "العربية (المملكة العربية السعودية)",
                _ => languageCode.ToUpperInvariant() // Fallback to uppercase code
            };
        }

        private LanguageInfo CreateFallbackLanguage()
        {
            return new LanguageInfo
            {
                LanguageCode = DEFAULT_LANGUAGE,
                DisplayName = "English (United States)",
                NativeName = "English (United States)",
                IsDefault = true
            };
        }
    }

    /// <summary>
    /// Information about an available language
    /// </summary>
    public class LanguageInfo
    {
        public string LanguageCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Metadata structure for language files
    /// </summary>
    public class LanguageMetadata
    {
        public string? LanguageCode { get; set; }
        public string? DisplayName { get; set; }
        public string? NativeName { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}