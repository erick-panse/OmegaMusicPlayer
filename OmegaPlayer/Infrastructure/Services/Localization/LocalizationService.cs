using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Features.Configuration.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OmegaPlayer.Infrastructure.Services
{
    /// <summary>
    /// Localization service with dynamic language detection
    /// </summary>
    public class LocalizationService : ObservableObject
    {
        private readonly IMessenger _messenger;
        private readonly LanguageDetectionService _languageDetectionService;
        private readonly Dictionary<string, Dictionary<string, string>> _translationCache = new();
        private CultureInfo _currentCulture;
        private const string DEFAULT_LANGUAGE = "en-US";

        // Current translations dictionary that UI can directly bind to
        private Dictionary<string, string> _currentTranslations = new();

        // This is what XAML will bind to directly
        public Dictionary<string, string> Translations => _currentTranslations;

        public string CurrentLanguage => _currentCulture?.Name ?? DEFAULT_LANGUAGE;

        /// <summary>
        /// Gets all available languages detected from resource files
        /// </summary>
        public List<LanguageInfo> AvailableLanguages => _languageDetectionService.GetAvailableLanguages();

        public LocalizationService(IMessenger messenger, LanguageDetectionService languageDetectionService)
        {
            _messenger = messenger;
            _languageDetectionService = languageDetectionService;

            // Register for language change messages
            _messenger.Register<LanguageChangedMessage>(this, (r, m) => ChangeLanguage(m.NewLanguage));

            // Initialize with default language
            var defaultLanguage = _languageDetectionService.GetDefaultLanguage();
            _currentCulture = CreateCultureInfo(defaultLanguage.LanguageCode);

            // Load the default language
            LoadLanguage(defaultLanguage.LanguageCode);
            UpdateCurrentTranslations();
        }

        /// <summary>
        /// Gets a localized string by key
        /// </summary>
        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            // First check current translations
            if (_currentTranslations.TryGetValue(key, out var translation))
            {
                return translation;
            }

            // If not found, return the key itself as fallback
            return key;
        }

        /// <summary>
        /// Indexer for backward compatibility
        /// </summary>
        public string this[string key] => GetString(key);

        /// <summary>
        /// Changes the application language
        /// </summary>
        public void ChangeLanguage(string languageCode)
        {
            try
            {
                // Validate and normalize language code
                languageCode = ValidateLanguageCode(languageCode);

                // If language hasn't changed, no need to update
                if (languageCode.Equals(_currentCulture?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Set the current culture
                _currentCulture = CreateCultureInfo(languageCode);

                // Load the language if it's not already loaded
                if (!_translationCache.ContainsKey(languageCode))
                {
                    LoadLanguage(languageCode);
                }

                // Update the current translations dictionary
                UpdateCurrentTranslations();
            }
            catch (Exception ex)
            {
                // If anything fails, fall back to default language
                var defaultLanguage = _languageDetectionService.GetDefaultLanguage();
                _currentCulture = CreateCultureInfo(defaultLanguage.LanguageCode);

                if (!_translationCache.ContainsKey(defaultLanguage.LanguageCode))
                {
                    LoadLanguage(defaultLanguage.LanguageCode);
                }
                UpdateCurrentTranslations();
            }
        }

        /// <summary>
        /// Gets language info for a specific language code
        /// </summary>
        public LanguageInfo GetLanguageInfo(string languageCode)
        {
            return _languageDetectionService.GetLanguageInfo(languageCode);
        }

        private string ValidateLanguageCode(string languageCode)
        {
            // Ensure we have a valid language code
            if (string.IsNullOrEmpty(languageCode))
            {
                return _languageDetectionService.GetDefaultLanguage().LanguageCode;
            }

            // Check if the language is available
            if (_languageDetectionService.IsLanguageAvailable(languageCode))
            {
                return languageCode;
            }

            // Try to find a close match (e.g., "en" -> "en-US")
            var availableLanguages = _languageDetectionService.GetAvailableLanguages();
            var closeMatch = availableLanguages.FirstOrDefault(l =>
                l.LanguageCode.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase) ||
                languageCode.StartsWith(l.LanguageCode.Split('-')[0], StringComparison.OrdinalIgnoreCase));

            if (closeMatch != null)
            {
                return closeMatch.LanguageCode;
            }

            // Fallback to default
            return _languageDetectionService.GetDefaultLanguage().LanguageCode;
        }

        private CultureInfo CreateCultureInfo(string languageCode)
        {
            try
            {
                return new CultureInfo(languageCode);
            }
            catch
            {
                // If the culture is not supported by the system, try with just the language part
                try
                {
                    var languagePart = languageCode.Split('-')[0];
                    return new CultureInfo(languagePart);
                }
                catch
                {
                    // Ultimate fallback
                    return new CultureInfo(DEFAULT_LANGUAGE);
                }
            }
        }

        private void UpdateCurrentTranslations()
        {
            // Run on UI thread to ensure UI updates properly
            Action updateAction = () =>
            {
                var languageCode = _currentCulture?.Name ?? DEFAULT_LANGUAGE;

                // Create a new dictionary (important for change detection)
                var newTranslations = new Dictionary<string, string>();

                // Get default language as fallback
                var defaultLanguage = _languageDetectionService.GetDefaultLanguage();
                if (!_translationCache.TryGetValue(defaultLanguage.LanguageCode, out var defaultTranslations))
                {
                    LoadLanguage(defaultLanguage.LanguageCode);
                    _translationCache.TryGetValue(defaultLanguage.LanguageCode, out defaultTranslations);
                }

                // First add all default language entries as fallback
                if (defaultTranslations != null)
                {
                    foreach (var kvp in defaultTranslations)
                    {
                        newTranslations[kvp.Key] = kvp.Value;
                    }
                }

                // Then override with current language entries
                if (!languageCode.Equals(defaultLanguage.LanguageCode, StringComparison.OrdinalIgnoreCase) &&
                    _translationCache.TryGetValue(languageCode, out var currentTranslations))
                {
                    foreach (var kvp in currentTranslations)
                    {
                        newTranslations[kvp.Key] = kvp.Value;
                    }
                }

                // Replace the entire dictionary
                _currentTranslations = newTranslations;

                // Notify that Translations property has changed
                OnPropertyChanged(nameof(Translations));
            };

            // Ensure we're on the UI thread
            if (Dispatcher.UIThread.CheckAccess())
                updateAction();
            else
                Dispatcher.UIThread.Post(updateAction);
        }

        private void LoadLanguage(string languageCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    languageCode = _languageDetectionService.GetDefaultLanguage().LanguageCode;
                }

                // Use the LanguageDetectionService to load translations
                var translations = _languageDetectionService.LoadLanguageTranslations(languageCode);

                if (translations != null && translations.Any())
                {
                    _translationCache[languageCode] = translations;
                }
                else
                {
                    _translationCache[languageCode] = new Dictionary<string, string>();
                }
            }
            catch (Exception)
            {
                _translationCache[languageCode] = new Dictionary<string, string>();
            }
        }
    }
}