using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Core.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using OmegaPlayer.Infrastructure.Services;
using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.Core.Models;

namespace OmegaPlayer.Features.Configuration.ViewModels
{
    public partial class ConfigViewModel : ViewModelBase
    {
        private readonly DirectoriesService _directoriesService;
        private readonly BlacklistedDirectoryService _blacklistService;
        private readonly ProfileManager _profileManager;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly IMessenger _messenger;
        private readonly IStorageProvider _storageProvider;

        [ObservableProperty]
        private ObservableCollection<Directories> _musicDirectories = new();

        [ObservableProperty]
        private ObservableCollection<BlacklistedDirectory> _blacklistedDirectories = new();

        [ObservableProperty]
        private ObservableCollection<string> _themes = new()
        {
            "Light", "Dark", "Custom"
        };

        [ObservableProperty]
        private ObservableCollection<string> _languages = new()
        {
            "English", "Spanish", "French", "German", "Japanese"
        };

        [ObservableProperty]
        private string _selectedTheme;
        public bool IsCustomTheme => SelectedTheme == PresetTheme.Custom.ToString();

        [ObservableProperty]
        private string _selectedLanguage;

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

        public ConfigViewModel(
            DirectoriesService directoriesService,
            BlacklistedDirectoryService blacklistService,
            ProfileManager profileManager,
            ProfileConfigurationService profileConfigService,
            GlobalConfigurationService globalConfigService,
            IMessenger messenger,
            IStorageProvider storageProvider)
        {
            _directoriesService = directoriesService;
            _blacklistService = blacklistService;
            _profileManager = profileManager;
            _profileConfigService = profileConfigService;
            _globalConfigService = globalConfigService;
            _messenger = messenger;
            _storageProvider = storageProvider;

            LoadSettingsAsync();
        }

        private async void LoadSettingsAsync()
        {
            try
            {
                // Load directories
                var directories = await _directoriesService.GetAllDirectories();
                MusicDirectories = new ObservableCollection<Directories>(directories);

                // Load blacklisted directories
                var blacklist = await _blacklistService.GetBlacklistedDirectories(_profileManager.CurrentProfile.ProfileID);
                BlacklistedDirectories = new ObservableCollection<BlacklistedDirectory>(blacklist);

                // Load profile config
                var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);
                DynamicPause = config.DynamicPause;
                
                // Parse theme configuration
                var themeConfig = ThemeConfiguration.FromJson(config.Theme);
                SelectedTheme = themeConfig.ThemeType.ToString();

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
                SelectedLanguage = globalConfig.LanguagePreference;
            }
            catch (Exception ex)
            {
                // Log error and show user-friendly message
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
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
            try
            {
                var folderPicker = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Music Directory",
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding directory: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RemoveDirectory(Directories directory)
        {
            try
            {
                await _directoriesService.DeleteDirectory(directory.DirID);
                MusicDirectories.Remove(directory);

                // Notify that directories have changed
                _messenger.Send(new DirectoriesChangedMessage());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing directory: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task AddBlacklist()
        {
            try
            {
                var folderPicker = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Directory to Blacklist",
                    AllowMultiple = false
                });

                if (folderPicker.Count > 0)
                {
                    var selectedFolder = folderPicker[0];
                    var blacklistId = await _blacklistService.AddBlacklistedDirectory(
                        _profileManager.CurrentProfile.ProfileID,
                        selectedFolder.Path.LocalPath);

                    BlacklistedDirectories.Add(new BlacklistedDirectory
                    {
                        BlacklistID = blacklistId,
                        ProfileID = _profileManager.CurrentProfile.ProfileID,
                        Path = selectedFolder.Path.LocalPath
                    });

                    // Notify that blacklist has changed
                    _messenger.Send(new BlacklistChangedMessage());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding blacklist: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RemoveBlacklist(BlacklistedDirectory blacklist)
        {
            try
            {
                await _blacklistService.RemoveBlacklistedDirectory(blacklist.BlacklistID);
                BlacklistedDirectories.Remove(blacklist);

                // Notify that blacklist has changed
                _messenger.Send(new BlacklistChangedMessage());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing blacklist: {ex.Message}");
            }
        }


        partial void OnDynamicPauseChanged(bool value)
        {
            _ = UpdateDynamicPauseSettingAsync(value);
        }

        private async Task UpdateDynamicPauseSettingAsync(bool value)
        {
            // Update the audio monitor service
            var trackControlVM = App.ServiceProvider.GetRequiredService<TrackControlViewModel>();
            trackControlVM.UpdateDynamicPause(value);

            await _profileConfigService.UpdatePlaybackSettings(
                _profileManager.CurrentProfile.ProfileID, value);

        }

        [RelayCommand]
        private async Task SaveCustomTheme()
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
        }

        partial void OnSelectedThemeChanged(string value) => HandleThemeChangeAsync(value);

        private async Task HandleThemeChangeAsync(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            OnPropertyChanged(nameof(IsCustomTheme));

            try
            {
                var profileConfig = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);
                var currentConfig = ThemeConfiguration.FromJson(profileConfig.Theme);

                // Create new theme config, preserving custom colors
                var themeConfig = new ThemeConfiguration
                {
                    // Update theme type
                    ThemeType = value switch
                    {
                        "Light" => PresetTheme.Light,
                        "Dark" => PresetTheme.Dark,
                        "Custom" => PresetTheme.Custom,
                        _ => PresetTheme.Dark
                    },
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
            catch (Exception ex)
            {
                // Do nothing for now
            }
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            _globalConfigService.UpdateLanguage(value).ConfigureAwait(false);

            // Notify UI of language change
            _messenger.Send(new LanguageChangedMessage(value));
        }
        // Add these partial methods to ConfigViewModel
        partial void OnWorkingMainStartColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingMainStartColor));
            }
        }

        partial void OnWorkingMainEndColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingMainEndColor));
            }
        }

        partial void OnWorkingSecondaryStartColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingSecondaryStartColor));
            }
        }

        partial void OnWorkingSecondaryEndColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingSecondaryEndColor));
            }
        }

        partial void OnWorkingAccentStartColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingAccentStartColor));
            }
        }

        partial void OnWorkingAccentEndColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingAccentEndColor));
            }
        }

        partial void OnWorkingTextStartColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingTextStartColor));
            }
        }

        partial void OnWorkingTextEndColorChanged(string value)
        {
            if (IsCustomTheme)
            {
                OnPropertyChanged(nameof(WorkingTextEndColor));
            }
        }
    }

    // Message classes for system events
    public record DirectoriesChangedMessage();
    public record BlacklistChangedMessage();
    public record ThemeChangedMessage(string NewTheme);
    public record LanguageChangedMessage(string NewLanguage);
}