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
                SelectedTheme = config.Theme;
                MainStartColor = config.MainStartColor;
                MainEndColor = config.MainEndColor;
                SecondaryStartColor = config.SecondaryStartColor;
                SecondaryEndColor = config.SecondaryEndColor;
                AccentStartColor = config.AccentStartColor;
                AccentEndColor = config.AccentEndColor;
                TextStartColor = config.TextStartColor;
                TextEndColor = config.TextEndColor;

                WorkingMainStartColor = config.MainStartColor;
                WorkingMainEndColor = config.MainEndColor;
                WorkingSecondaryStartColor = config.SecondaryStartColor;
                WorkingSecondaryEndColor = config.SecondaryEndColor;
                WorkingAccentStartColor = config.AccentStartColor;
                WorkingAccentEndColor = config.AccentEndColor;
                WorkingTextStartColor = config.TextStartColor;
                WorkingTextEndColor = config.TextEndColor;


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
            // Update the stored theme colors
            MainStartColor = WorkingMainStartColor;
            MainEndColor = WorkingMainEndColor;
            SecondaryStartColor = WorkingSecondaryStartColor;
            SecondaryEndColor = WorkingSecondaryEndColor;
            AccentStartColor = WorkingAccentStartColor;
            AccentEndColor = WorkingAccentEndColor;
            TextStartColor = WorkingTextStartColor;
            TextEndColor = WorkingTextEndColor;

            // Update theme in database and apply changes
            await _profileConfigService.UpdateProfileTheme(
                _profileManager.CurrentProfile.ProfileID,
                SelectedTheme,
                MainStartColor,
                MainEndColor,
                SecondaryStartColor,
                SecondaryEndColor,
                AccentStartColor,
                AccentEndColor,
                TextStartColor,
                TextEndColor);

            // Notify about theme change
            var themeConfig = new ThemeConfiguration
            {
                ThemeType = PresetTheme.Custom,
                MainStartColor = MainStartColor,
                MainEndColor = MainEndColor,
                SecondaryStartColor = SecondaryStartColor,
                SecondaryEndColor = SecondaryEndColor,
                AccentStartColor = AccentStartColor,
                AccentEndColor = AccentEndColor,
                TextStartColor = TextStartColor,
                TextEndColor = TextEndColor
            };

            _messenger.Send(new ThemeUpdatedMessage(themeConfig));
        }
        private void UpdateThemeColors()
        {
            _profileConfigService.UpdateProfileTheme(
                _profileManager.CurrentProfile.ProfileID,
                SelectedTheme,
                MainStartColor,
                MainEndColor,
                SecondaryStartColor,
                SecondaryEndColor,
                AccentStartColor,
                AccentEndColor,
                TextStartColor,
                TextEndColor).ConfigureAwait(false);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomTheme));

            if (string.IsNullOrEmpty(value)) return;

            // Get current config or create new one
            var themeConfig = new ThemeConfiguration
            {
                ThemeType = value switch
                {
                    "Light" => PresetTheme.Light,
                    "Dark" => PresetTheme.Dark,
                    "Custom" => PresetTheme.Custom,
                    _ => PresetTheme.Dark
                }
            };

            // If custom theme, use the color values from the UI
            if (themeConfig.ThemeType == PresetTheme.Custom)
            {
                themeConfig.MainStartColor = MainStartColor;
                themeConfig.MainEndColor = MainEndColor;
                themeConfig.SecondaryStartColor = SecondaryStartColor;
                themeConfig.SecondaryEndColor = SecondaryEndColor;
                themeConfig.AccentStartColor = AccentStartColor;
                themeConfig.AccentEndColor = AccentEndColor;
                themeConfig.TextStartColor = TextStartColor;
                themeConfig.TextEndColor = TextEndColor;
            }

            // Convert to JSON for storage
            string themeJson = themeConfig.ToJson();

            // Update profile config
            _profileConfigService.UpdateProfileTheme(
                _profileManager.CurrentProfile.ProfileID,
                value,  // theme name (Light/Dark/Custom)
                MainStartColor,MainEndColor,
                SecondaryStartColor,SecondaryEndColor,
                AccentStartColor,AccentEndColor,
                TextStartColor,TextEndColor).ConfigureAwait(false);

            // Notify about theme change
            _messenger.Send(new ThemeUpdatedMessage(themeConfig));
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