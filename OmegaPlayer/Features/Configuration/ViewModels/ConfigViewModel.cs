using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Core.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using OmegaPlayer.Infrastructure.Services;
using System;
using CommunityToolkit.Mvvm.Messaging;

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
        private ObservableCollection<string> _playbackSpeeds = new()
        {
            "0.5x", "0.75x", "1.0x", "1.25x", "1.5x", "2.0x"
        };

        [ObservableProperty]
        private ObservableCollection<string> _themes = new()
        {
            "Light", "Dark", "System"
        };

        [ObservableProperty]
        private ObservableCollection<string> _languages = new()
        {
            "English", "Spanish", "French", "German", "Japanese"
        };

        [ObservableProperty]
        private string _selectedPlaybackSpeed = "1.0x";

        [ObservableProperty]
        private string _selectedTheme;

        [ObservableProperty]
        private string _selectedLanguage;

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
                SelectedPlaybackSpeed = $"{config.DefaultPlaybackSpeed}x";
                SelectedTheme = config.Theme;

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

        partial void OnSelectedPlaybackSpeedChanged(string value)
        {
            if (value == null) {return; }

            if (float.TryParse(value.TrimEnd('x'), out float speed))
            {
                _profileConfigService.UpdatePlaybackSettings(
                    _profileManager.CurrentProfile.ProfileID,
                    speed,
                    DynamicPause,
                    null).ConfigureAwait(false);
            }
        }

        partial void OnDynamicPauseChanged(bool value)
        {
            _profileConfigService.UpdatePlaybackSettings(
                _profileManager.CurrentProfile.ProfileID,
                float.Parse(SelectedPlaybackSpeed.TrimEnd('x')),
                value,
                null).ConfigureAwait(false);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            _profileConfigService.UpdateProfileTheme(
                _profileManager.CurrentProfile.ProfileID,
                value,
                null,  // Maintain existing colors
                null).ConfigureAwait(false);

            // Notify UI of theme change
            _messenger.Send(new ThemeChangedMessage(value));
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            _globalConfigService.UpdateLanguage(value).ConfigureAwait(false);

            // Notify UI of language change
            _messenger.Send(new LanguageChangedMessage(value));
        }
    }

    // Message classes for system events
    public record DirectoriesChangedMessage();
    public record BlacklistChangedMessage();
    public record ThemeChangedMessage(string NewTheme);
    public record LanguageChangedMessage(string NewLanguage);
}