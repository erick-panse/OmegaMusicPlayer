using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Core.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using OmegaPlayer.Infrastructure.Services;
using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Features.Profile.ViewModels;
using System.Linq;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Configuration.ViewModels
{
    public partial class ConfigViewModel : ViewModelBase
    {
        private readonly DirectoriesService _directoriesService;
        private readonly ProfileManager _profileManager;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly LocalizationService _localizationService;
        private readonly IMessenger _messenger;
        private readonly IStorageProvider _storageProvider;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private ObservableCollection<Directories> _musicDirectories = new();

        [ObservableProperty]
        private ObservableCollection<BlacklistFolderViewModel> _blacklistedDirectories = new();

        [ObservableProperty]
        private ObservableCollection<string> _themes = new();

        [ObservableProperty]
        private ObservableCollection<LanguageOption> _languages = new();

        [ObservableProperty]
        private string _selectedTheme;
        public bool IsCustomTheme => SelectedTheme == _localizationService["ThemeCustom"];

        [ObservableProperty]
        private LanguageOption _selectedLanguage;

        [ObservableProperty]
        private string _mainStartColor = "#08142E";

        [ObservableProperty]
        private string _mainEndColor = "#0D1117";

        [ObservableProperty]
        private string _secondaryStartColor = "#41295a";

        [ObservableProperty]
        private string _secondaryEndColor = "#2F0743";

        [ObservableProperty]
        private string _accentStartColor = "#0000FF";

        [ObservableProperty]
        private string _accentEndColor = "#EE82EE";

        [ObservableProperty]
        private string _textStartColor = "#61045F";

        [ObservableProperty]
        private string _textEndColor = "#aa0744";

        // Colors used in the color picker to preview gradients
        [ObservableProperty]
        private string _workingMainStartColor = "#08142E";

        [ObservableProperty]
        private string _workingMainEndColor = "#0D1117";

        [ObservableProperty]
        private string _workingSecondaryStartColor = "#41295a";

        [ObservableProperty]
        private string _workingSecondaryEndColor = "#2F0743";

        [ObservableProperty]
        private string _workingAccentStartColor = "#0000FF";

        [ObservableProperty]
        private string _workingAccentEndColor = "#EE82EE";

        [ObservableProperty]
        private string _workingTextStartColor = "#61045F";

        [ObservableProperty]
        private string _workingTextEndColor = "#aa0744";

        [ObservableProperty]
        private bool _dynamicPause;

        [ObservableProperty]
        private bool _isMusicExpanded = false;

        [ObservableProperty]
        private bool _isBlacklistExpanded = false;

        [ObservableProperty]
        private bool _isLoading = false;

        private PresetTheme _currentThemeType = PresetTheme.Dark;

        // flag to prevent recursive updates
        private bool _isUpdating = false;

        public ConfigViewModel(
            DirectoriesService directoriesService,
            ProfileManager profileManager,
            ProfileConfigurationService profileConfigService,
            GlobalConfigurationService globalConfigService,
            LocalizationService localizationService,
            IMessenger messenger,
            IStorageProvider storageProvider,
            IErrorHandlingService errorHandlingService)
        {
            _directoriesService = directoriesService;
            _profileManager = profileManager;
            _profileConfigService = profileConfigService;
            _globalConfigService = globalConfigService;
            _localizationService = localizationService;
            _messenger = messenger;
            _storageProvider = storageProvider;
            _errorHandlingService = errorHandlingService;

            // Initialize collections just once
            InitializeCollections();

            LoadSettingsAsync();

            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileSwitch(m));
            _messenger.Register<LanguageChangedMessage>(this, (r, m) => UpdateDisplayTexts());
        }

        private void InitializeCollections()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                // Set up language options
                Languages.Add(new LanguageOption { DisplayName = _localizationService["English"], LanguageCode = "en" });
                Languages.Add(new LanguageOption { DisplayName = _localizationService["Spanish"], LanguageCode = "es" });
                Languages.Add(new LanguageOption { DisplayName = _localizationService["French"], LanguageCode = "fr" });
                Languages.Add(new LanguageOption { DisplayName = _localizationService["German"], LanguageCode = "de" });
                Languages.Add(new LanguageOption { DisplayName = _localizationService["Japanese"], LanguageCode = "ja" });

                // Set up theme options
                Themes.Add(_localizationService["ThemeDarkNeon"]);
                Themes.Add(_localizationService["ThemeSunset"]);
                Themes.Add(_localizationService["ThemeDark"]);
                Themes.Add(_localizationService["ThemeLight"]);
                Themes.Add(_localizationService["ThemeCustom"]);
            }, "Initializing configuration collections");
        }

        private async void UpdateDisplayTexts()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (_isUpdating) return;

                _isUpdating = true;
                try
                {
                    // Load profile config
                    var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);

                    // Parse theme configuration
                    var themeConfig = ThemeConfiguration.FromJson(config.Theme);

                    // IMPORTANT: Store the current theme type
                    _currentThemeType = themeConfig.ThemeType;

                    var currentLanguage = SelectedLanguage;

                    // IMPORTANT: Update theme text instead of recreating collection
                    for (int i = 0; i < Themes.Count; i++)
                    {
                        string themeText = i switch
                        {
                            0 => _localizationService["ThemeDarkNeon"],
                            1 => _localizationService["ThemeSunset"],
                            2 => _localizationService["ThemeDark"],
                            3 => _localizationService["ThemeLight"],
                            4 => _localizationService["ThemeCustom"],
                            _ => Themes[i]
                        };

                        // Only update if the text has changed
                        if (Themes[i] != themeText)
                        {
                            Themes[i] = themeText;
                        }
                    }

                    // Restore theme selection based on theme type
                    string newThemeName = _currentThemeType switch
                    {
                        PresetTheme.DarkNeon => _localizationService["ThemeDarkNeon"],
                        PresetTheme.Sunset => _localizationService["ThemeSunset"],
                        PresetTheme.Dark => _localizationService["ThemeDark"],
                        PresetTheme.Light => _localizationService["ThemeLight"],
                        PresetTheme.Custom => _localizationService["ThemeCustom"],
                        _ => _localizationService["ThemeDark"]
                    };

                    if (SelectedTheme != newThemeName)
                    {
                        SelectedTheme = newThemeName;
                    }

                    // Update language display names in-place
                    foreach (var language in Languages)
                    {
                        string displayName = language.LanguageCode switch
                        {
                            "en" => _localizationService["English"],
                            "es" => _localizationService["Spanish"],
                            "fr" => _localizationService["French"],
                            "de" => _localizationService["German"],
                            "ja" => _localizationService["Japanese"],
                            _ => language.DisplayName
                        };

                        // Only update if the display name has changed
                        if (language.DisplayName != displayName)
                        {
                            language.DisplayName = displayName;
                        }
                    }

                    // No need to reset SelectedLanguage
                }
                finally
                {
                    _isUpdating = false;
                }
            }, "Updating localized display texts in configuration");
        }

        private PresetTheme GetThemeEnumFromString(string themeName)
        {
            return _errorHandlingService.SafeExecute(() =>
            {
                // Check against localized names
                if (themeName == _localizationService["ThemeDarkNeon"]) return PresetTheme.DarkNeon;
                if (themeName == _localizationService["ThemeSunset"]) return PresetTheme.Sunset;
                if (themeName == _localizationService["ThemeLight"]) return PresetTheme.Light;
                if (themeName == _localizationService["ThemeDark"]) return PresetTheme.Dark;
                if (themeName == _localizationService["ThemeCustom"]) return PresetTheme.Custom;

                // Fallback to checking English names (for backward compatibility)
                if (themeName == "Dark Neon") return PresetTheme.DarkNeon;
                if (themeName == "Sunset") return PresetTheme.Sunset;
                if (themeName == "Light") return PresetTheme.Light;
                if (themeName == "Dark") return PresetTheme.Dark;
                if (themeName == "Custom") return PresetTheme.Custom;

                // Default
                return PresetTheme.Dark;
            }, "Determining theme type from name", PresetTheme.Dark);
        }

        private void HandleProfileSwitch(ProfileUpdateMessage message)
        {
            LoadSettingsAsync();
        }

        private async void LoadSettingsAsync()
        {
            IsLoading = true;

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                try
                {
                    // Ensure ProfileManager is initialized
                    await _profileManager.InitializeAsync();

                    // Load directories
                    var directories = await _directoriesService.GetAllDirectories();
                    MusicDirectories = new ObservableCollection<Directories>(directories);

                    // Load profile config
                    var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);

                    // Load blacklisted directories directly from profile config
                    LoadBlacklistedDirectories(config);

                    DynamicPause = config.DynamicPause;

                    // Parse theme configuration
                    var themeConfig = ThemeConfiguration.FromJson(config.Theme);

                    // IMPORTANT: Store the current theme type
                    _currentThemeType = themeConfig.ThemeType;

                    // Map the theme type enum to the localized string
                    string localizedThemeName = themeConfig.ThemeType switch
                    {
                        PresetTheme.DarkNeon => _localizationService["ThemeDarkNeon"],
                        PresetTheme.Sunset => _localizationService["ThemeSunset"],
                        PresetTheme.Light => _localizationService["ThemeLight"],
                        PresetTheme.Dark => _localizationService["ThemeDark"],
                        PresetTheme.Custom => _localizationService["ThemeCustom"],
                        _ => _localizationService["ThemeDark"]
                    };

                    SelectedTheme = localizedThemeName;

                    MainStartColor = themeConfig.MainStartColor;
                    MainEndColor = themeConfig.MainEndColor;
                    SecondaryStartColor = themeConfig.SecondaryStartColor;
                    SecondaryEndColor = themeConfig.SecondaryEndColor;
                    AccentStartColor = themeConfig.AccentStartColor;
                    AccentEndColor = themeConfig.AccentEndColor;
                    TextStartColor = themeConfig.TextStartColor;
                    TextEndColor = themeConfig.TextEndColor;

                    WorkingMainStartColor = themeConfig.MainStartColor;
                    WorkingMainEndColor = themeConfig.MainEndColor;
                    WorkingSecondaryStartColor = themeConfig.SecondaryStartColor;
                    WorkingSecondaryEndColor = themeConfig.SecondaryEndColor;
                    WorkingAccentStartColor = themeConfig.AccentStartColor;
                    WorkingAccentEndColor = themeConfig.AccentEndColor;
                    WorkingTextStartColor = themeConfig.TextStartColor;
                    WorkingTextEndColor = themeConfig.TextEndColor;

                    // Load global config
                    var globalConfig = await _globalConfigService.GetGlobalConfig();

                    // Set selected language
                    SelectedLanguage = Languages.FirstOrDefault(l => l.LanguageCode == globalConfig.LanguagePreference)
                        ?? Languages.First(); // Default to first language if not found
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error loading configuration settings",
                        "Could not load all configuration settings. Some values may be using defaults.",
                        ex,
                        true);
                }
            }, "Loading configuration settings", ErrorSeverity.NonCritical);

            IsLoading = false;
        }

        private void LoadBlacklistedDirectories(ProfileConfig config)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                BlacklistedDirectories.Clear();

                if (config.BlacklistDirectory != null)
                {
                    // Filter out null or empty paths and create view models
                    var blacklistViewModels = config.BlacklistDirectory
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => new BlacklistFolderViewModel { Path = path })
                        .ToList();

                    // Add to observable collection
                    foreach (var vm in blacklistViewModels)
                    {
                        BlacklistedDirectories.Add(vm);
                    }
                }
            }, "Loading blacklisted directories", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private void ToggleMusicExpanded()
        {
            IsMusicExpanded = !IsMusicExpanded;
            if (IsMusicExpanded)
            {
                LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private void ToggleBlacklistExpanded()
        {
            IsBlacklistExpanded = !IsBlacklistExpanded;
            if (IsBlacklistExpanded)
            {
                LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private async Task AddDirectory()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                var folderPicker = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = _localizationService["SelectMusicDirectory"],
                    AllowMultiple = false
                });

                if (folderPicker.Count > 0)
                {
                    var selectedFolder = folderPicker[0];
                    var directory = new Directories { DirPath = selectedFolder.Path.LocalPath };
                    var dirId = await _directoriesService.AddDirectory(directory);
                    directory.DirID = dirId;
                    MusicDirectories.Add(directory);

                    // Notify that directories have changed
                    _messenger.Send(new DirectoriesChangedMessage());
                }
            }, "Adding music directory", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task RemoveDirectory(Directories directory)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                await _directoriesService.DeleteDirectory(directory.DirID);
                MusicDirectories.Remove(directory);

                // Notify that directories have changed
                _messenger.Send(new DirectoriesChangedMessage());
            }, "Removing music directory", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task AddBlacklist()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                var folderPicker = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = _localizationService["SelectBlacklistDirectory"],
                    AllowMultiple = false
                });

                if (folderPicker.Count > 0)
                {
                    var selectedFolder = folderPicker[0];
                    string path = selectedFolder.Path.LocalPath;

                    // Get current profile config
                    var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);

                    // Get current blacklist
                    var currentBlacklist = config.BlacklistDirectory?.ToList() ?? new List<string>();

                    // Check if path is already blacklisted (case-insensitive)
                    if (!currentBlacklist.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Add to UI collection
                        BlacklistedDirectories.Add(new BlacklistFolderViewModel
                        {
                            Path = path
                        });

                        // Add to blacklist array
                        currentBlacklist.Add(path);

                        // Update profile config
                        await _profileConfigService.UpdateBlacklist(_profileManager.CurrentProfile.ProfileID, currentBlacklist.ToArray());

                        // Notify that blacklist has changed
                        _messenger.Send(new BlacklistChangedMessage());
                    }
                    else
                    {
                        // Path already exists in blacklist
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Directory already blacklisted",
                            $"The directory '{path}' is already in the blacklist.",
                            null,
                            true);
                    }
                }
            }, "Adding blacklisted directory", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task RemoveBlacklist(BlacklistFolderViewModel blacklist)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (blacklist == null) return;

                // Remove from UI collection
                BlacklistedDirectories.Remove(blacklist);

                // Get current profile config
                var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);

                // Get current blacklist excluding the removed path (case-insensitive)
                var updatedBlacklist = (config.BlacklistDirectory ?? Array.Empty<string>())
                    .Where(p => !string.Equals(p, blacklist.Path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                // Update profile config
                await _profileConfigService.UpdateBlacklist(
                    _profileManager.CurrentProfile.ProfileID,
                    updatedBlacklist);

                // Notify that blacklist has changed
                _messenger.Send(new BlacklistChangedMessage());
            }, "Removing blacklisted directory", ErrorSeverity.NonCritical);
        }

        partial void OnDynamicPauseChanged(bool value)
        {
            _ = UpdateDynamicPauseSettingAsync(value);
        }

        private async Task UpdateDynamicPauseSettingAsync(bool value)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // Update the audio monitor service
                var trackControlVM = App.ServiceProvider.GetRequiredService<TrackControlViewModel>();
                trackControlVM.UpdateDynamicPause(value);

                await _profileConfigService.UpdatePlaybackSettings(
                    _profileManager.CurrentProfile.ProfileID, value);
            }, "Updating dynamic pause setting", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task SaveCustomTheme()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // Create theme configuration with current working colors
                var themeConfig = new ThemeConfiguration
                {
                    ThemeType = PresetTheme.Custom,
                    MainStartColor = WorkingMainStartColor,
                    MainEndColor = WorkingMainEndColor,
                    SecondaryStartColor = WorkingSecondaryStartColor,
                    SecondaryEndColor = WorkingSecondaryEndColor,
                    AccentStartColor = WorkingAccentStartColor,
                    AccentEndColor = WorkingAccentEndColor,
                    TextStartColor = WorkingTextStartColor,
                    TextEndColor = WorkingTextEndColor
                };

                // Update the stored theme values
                MainStartColor = WorkingMainStartColor;
                MainEndColor = WorkingMainEndColor;
                SecondaryStartColor = WorkingSecondaryStartColor;
                SecondaryEndColor = WorkingSecondaryEndColor;
                AccentStartColor = WorkingAccentStartColor;
                AccentEndColor = WorkingAccentEndColor;
                TextStartColor = WorkingTextStartColor;
                TextEndColor = WorkingTextEndColor;

                // Save configuration
                await _profileConfigService.UpdateProfileTheme(_profileManager.CurrentProfile.ProfileID, themeConfig);

                // Notify about theme change
                _messenger.Send(new ThemeUpdatedMessage(themeConfig));
            }, "Saving custom theme", ErrorSeverity.NonCritical);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || _isUpdating) return;

            HandleThemeChangeAsync(value);
        }

        private async Task HandleThemeChangeAsync(string value)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (string.IsNullOrEmpty(value)) return;
                OnPropertyChanged(nameof(IsCustomTheme));

                var profileConfig = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);
                var currentConfig = ThemeConfiguration.FromJson(profileConfig.Theme);

                // Get the theme type from the value
                var newThemeType = GetThemeEnumFromString(value);

                // Only proceed if the theme type is actually changing
                if (currentConfig.ThemeType != newThemeType)
                {
                    // Create new theme config, preserving custom colors
                    var themeConfig = new ThemeConfiguration
                    {
                        // Update theme type
                        ThemeType = newThemeType,

                        // Preserve custom colors from current config
                        MainStartColor = currentConfig.MainStartColor ?? ThemeConfiguration.GetDefaultCustomTheme().MainStartColor,
                        MainEndColor = currentConfig.MainEndColor ?? ThemeConfiguration.GetDefaultCustomTheme().MainEndColor,
                        SecondaryStartColor = currentConfig.SecondaryStartColor ?? ThemeConfiguration.GetDefaultCustomTheme().SecondaryStartColor,
                        SecondaryEndColor = currentConfig.SecondaryEndColor ?? ThemeConfiguration.GetDefaultCustomTheme().SecondaryEndColor,
                        AccentStartColor = currentConfig.AccentStartColor ?? ThemeConfiguration.GetDefaultCustomTheme().AccentStartColor,
                        AccentEndColor = currentConfig.AccentEndColor ?? ThemeConfiguration.GetDefaultCustomTheme().AccentEndColor,
                        TextStartColor = currentConfig.TextStartColor ?? ThemeConfiguration.GetDefaultCustomTheme().TextStartColor,
                        TextEndColor = currentConfig.TextEndColor ?? ThemeConfiguration.GetDefaultCustomTheme().TextEndColor
                    };

                    // Update the current theme type field
                    _currentThemeType = newThemeType;

                    // If switching to custom theme, update working colors
                    if (themeConfig.ThemeType == PresetTheme.Custom)
                    {
                        WorkingMainStartColor = themeConfig.MainStartColor;
                        WorkingMainEndColor = themeConfig.MainEndColor;
                        WorkingSecondaryStartColor = themeConfig.SecondaryStartColor;
                        WorkingSecondaryEndColor = themeConfig.SecondaryEndColor;
                        WorkingAccentStartColor = themeConfig.AccentStartColor;
                        WorkingAccentEndColor = themeConfig.AccentEndColor;
                        WorkingTextStartColor = themeConfig.TextStartColor;
                        WorkingTextEndColor = themeConfig.TextEndColor;
                    }

                    // Save configuration
                    await _profileConfigService.UpdateProfileTheme(_profileManager.CurrentProfile.ProfileID, themeConfig);

                    // Apply theme immediately through ThemeService
                    var themeService = App.ServiceProvider.GetRequiredService<ThemeService>();

                    if (themeConfig.ThemeType == PresetTheme.Custom)
                    {
                        themeService.ApplyTheme(themeConfig.ToThemeColors());
                    }
                    else
                    {
                        themeService.ApplyPresetTheme(themeConfig.ThemeType);
                    }

                    // Notify about theme change
                    _messenger.Send(new ThemeUpdatedMessage(themeConfig));
                }
            }, "Handling theme change", ErrorSeverity.NonCritical);
        }

        partial void OnSelectedLanguageChanged(LanguageOption value)
        {
            if (value == null || _isUpdating) return;

            _errorHandlingService.SafeExecute(() =>
            {
                // Update the language preference in global config
                _globalConfigService.UpdateLanguage(value.LanguageCode).ConfigureAwait(false);

                // Notify UI of language change
                _messenger.Send(new LanguageChangedMessage(value.LanguageCode));
            }, "Updating application language", ErrorSeverity.NonCritical);
        }

        // Property change handlers for color pickers (already implemented, just preserved)
        partial void OnWorkingMainStartColorChanged(string value) => UpdateColorProperty(nameof(WorkingMainStartColor));
        partial void OnWorkingMainEndColorChanged(string value) => UpdateColorProperty(nameof(WorkingMainEndColor));
        partial void OnWorkingSecondaryStartColorChanged(string value) => UpdateColorProperty(nameof(WorkingSecondaryStartColor));
        partial void OnWorkingSecondaryEndColorChanged(string value) => UpdateColorProperty(nameof(WorkingSecondaryEndColor));
        partial void OnWorkingAccentStartColorChanged(string value) => UpdateColorProperty(nameof(WorkingAccentStartColor));
        partial void OnWorkingAccentEndColorChanged(string value) => UpdateColorProperty(nameof(WorkingAccentEndColor));
        partial void OnWorkingTextStartColorChanged(string value) => UpdateColorProperty(nameof(WorkingTextStartColor));
        partial void OnWorkingTextEndColorChanged(string value) => UpdateColorProperty(nameof(WorkingTextEndColor));

        private void UpdateColorProperty(string propertyName)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(propertyName);
            }
        }
    }

    // Language option class for better display and selection
    public class LanguageOption : ObservableObject
    {
        private string _displayName;
        private string _languageCode;

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string LanguageCode
        {
            get => _languageCode;
            set => SetProperty(ref _languageCode, value);
        }

        public override string ToString() => DisplayName;
    }

    // View model for blacklist folders, replacing the old model
    public class BlacklistFolderViewModel : ObservableObject
    {
        private string _path;

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string FolderName => System.IO.Path.GetFileName(_path) ?? _path;
    }

    // Keep these record classes unchanged
    public record DirectoriesChangedMessage();
    public record BlacklistChangedMessage();
    public record ThemeChangedMessage(string NewTheme);
    public record LanguageChangedMessage(string NewLanguage);
}