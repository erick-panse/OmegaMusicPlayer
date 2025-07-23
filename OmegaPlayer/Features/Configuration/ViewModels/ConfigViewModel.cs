using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Infrastructure.Data;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Database;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Configuration.ViewModels
{
    public partial class ConfigViewModel : ViewModelBase
    {
        private readonly DirectoriesService _directoriesService;
        private readonly ProfileManager _profileManager;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly LocalizationService _localizationService;
        private readonly LibraryMaintenanceService _maintenanceService;
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
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
        private bool _isMusicExpanded = true;

        [ObservableProperty]
        private bool _isBlacklistExpanded = true;

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
            LibraryMaintenanceService maintenanceService,
            DirectoryScannerService directoryScannerService,
            AllTracksRepository allTracksRepository,
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _directoriesService = directoriesService;
            _profileManager = profileManager;
            _profileConfigService = profileConfigService;
            _globalConfigService = globalConfigService;
            _localizationService = localizationService;
            _maintenanceService = maintenanceService;
            _directoryScannerService = directoryScannerService;
            _allTracksRepository = allTracksRepository;
            _contextFactory = contextFactory;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            // Get StorageProvider from MainWindow using proper casting
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            _storageProvider = mainWindow?.StorageProvider;

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
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    var config = await _profileConfigService.GetProfileConfig(profile.ProfileID);

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
                    // Load directories
                    var directories = await _directoriesService.GetAllDirectories();
                    MusicDirectories = new ObservableCollection<Directories>(directories);

                    // Load profile config
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    var config = await _profileConfigService.GetProfileConfig(profile.ProfileID);

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
                // Store the directory path for background cleanup
                string directoryPath = directory.DirPath;

                // Remove from UI and database first
                await _directoriesService.DeleteDirectory(directory.DirID);
                MusicDirectories.Remove(directory);

                // Fire-and-forget background cleanup of tracks in this directory
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _maintenanceService.CleanupTracksInDirectory(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Background directory cleanup failed",
                            $"Failed to clean up tracks from removed directory: {directoryPath}",
                            ex,
                            false);
                    }
                });

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

                    var profile = await _profileManager.GetCurrentProfileAsync();

                    // Use the service method that handles cache properly
                    await _profileConfigService.AddBlacklistDirectory(profile.ProfileID, path);

                    // Reload the blacklist from fresh data
                    await ReloadBlacklistFromConfig();
                }
            }, "Adding blacklisted directory", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task RemoveBlacklist(BlacklistFolderViewModel blacklist)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (blacklist == null) return;

                var profile = await _profileManager.GetCurrentProfileAsync();

                // Use the service method that handles cache properly
                await _profileConfigService.RemoveBlacklistDirectory(profile.ProfileID, blacklist.Path);

                // Reload the blacklist from fresh data
                await ReloadBlacklistFromConfig();

            }, "Removing blacklisted directory", ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Reloads blacklist from fresh config data
        /// </summary>
        private async Task ReloadBlacklistFromConfig()
        {
            var profile = await _profileManager.GetCurrentProfileAsync();

            // Force fresh data by invalidating cache first
            _profileConfigService.InvalidateCache(profile.ProfileID);

            var config = await _profileConfigService.GetProfileConfig(profile.ProfileID);

            // Update UI with fresh data
            BlacklistedDirectories.Clear();
            LoadBlacklistedDirectories(config);
        }

        [RelayCommand]
        private async Task SyncLibrary()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // Check if scanning is already in progress
                if (_directoryScannerService.isScanningInProgress)
                {
                    _errorHandlingService.LogInfo(
                        "Scan already in progress",
                        "A library scan is already running. Please wait for it to complete.",
                        true);
                    return;
                }

                // Reset the last scan time to bypass interval restriction
                _directoryScannerService.lastFullScanTime = DateTime.MinValue;

                IsLoading = true;

                // Get current profile and directories
                var profile = await _profileManager.GetCurrentProfileAsync();
                var directories = await _directoriesService.GetAllDirectories();

                if (directories == null || !directories.Any())
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "No directories to sync",
                        "Please add music directories before syncing the library.",
                        null,
                        true);
                    return;
                }

                // Trigger directory scanning which will populate track metadata
                await _directoryScannerService.ScanDirectoriesAsync(directories, profile.ProfileID);

                _errorHandlingService.LogInfo(
                    "Library sync completed",
                    "Music library has been synchronized successfully.",
                    true);

                IsLoading = false;
            }, "Synchronizing music library", ErrorSeverity.NonCritical);
        }

        [RelayCommand]
        private async Task ClearLibrary()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // Show confirmation dialog
                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (mainWindow == null) return;

                var result = await CustomMessageBox.Show(
                    mainWindow,
                    _localizationService["ClearLibraryConfirmTitle"],
                    _localizationService["ClearLibraryConfirmMessage"],
                    CustomMessageBox.MessageBoxButtons.YesNo);

                if (result != CustomMessageBox.MessageBoxResult.Yes)
                {
                    return;
                }

                IsLoading = true;

                var trackControlViewModel = App.ServiceProvider.GetRequiredService<TrackControlViewModel>();
                var trackQueueViewModel = App.ServiceProvider.GetRequiredService<TrackQueueViewModel>();
                var queueService = App.ServiceProvider.GetRequiredService<QueueService>();

                if (trackControlViewModel == null || trackQueueViewModel == null || queueService == null)
                    return;

                _errorHandlingService.LogInfo(
                    "Starting library cleanup",
                    "Removing all music data while preserving profiles and settings...",
                    true);

                // Stop playback if something is playing
                if (trackControlViewModel.IsPlaying == PlaybackState.Playing)
                {
                    trackControlViewModel.StopPlayback();
                }

                // Clear the queue from memory
                trackQueueViewModel.NowPlayingQueue.Clear();

                // Clear the current track
                trackQueueViewModel.CurrentTrack = null;
                await trackControlViewModel.UpdateTrackInfo();

                // Clear from database
                var profile = await _profileManager.GetCurrentProfileAsync();
                await queueService.ClearCurrentQueueForProfile(profile.ProfileID);

                // Clear all music data from database and UI
                await ClearAllMusicData();
                MusicDirectories.Clear();
                _directoriesService.InvalidateCache();

                // Update queue durations
                trackQueueViewModel.UpdateDurations();

                // Send notification to update other components
                _messenger.Send(new TrackQueueUpdateMessage(
                    null,
                    new ObservableCollection<TrackDisplayModel>(),
                    -1));

                // Invalidate caches to reflect the clean state
                _allTracksRepository.InvalidateAllCaches();

                _errorHandlingService.LogInfo(
                    "Library cleanup completed",
                    "All music data has been removed. Profiles and settings have been preserved.",
                    true);

                // Notify that the library has been cleaned
                _messenger.Send(new DirectoriesChangedMessage());

                IsLoading = false;

            }, "Cleaning music library", ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Clears all music-related data from the database while preserving profiles and configurations
        /// </summary>
        private async Task ClearAllMusicData()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    // Get all profile photo IDs to preserve them
                    var profilePhotoIds = await context.Profiles
                        .Where(p => p.PhotoId != null)
                        .Select(p => p.PhotoId.Value)
                        .ToListAsync();

                    // Delete all music-related data in order (respecting foreign key constraints)

                    // 1. Delete junction table records first
                    await context.TrackArtists.ExecuteDeleteAsync();
                    await context.TrackGenres.ExecuteDeleteAsync();
                    await context.PlaylistTracks.ExecuteDeleteAsync();
                    await context.QueueTracks.ExecuteDeleteAsync();

                    // 2. Delete user interaction data
                    await context.PlayHistories.ExecuteDeleteAsync();
                    await context.PlayCounts.ExecuteDeleteAsync();
                    await context.Likes.ExecuteDeleteAsync();

                    // 3. Delete queue and playlist data
                    await context.CurrentQueues.ExecuteDeleteAsync();
                    await context.Playlists.ExecuteDeleteAsync();

                    // 4. Delete main music entities
                    await context.Tracks.ExecuteDeleteAsync();
                    await context.Albums.ExecuteDeleteAsync();
                    await context.Artists.ExecuteDeleteAsync();
                    await context.Genres.ExecuteDeleteAsync();

                    // 5. Delete media files except profile photos
                    var mediaToDelete = await context.Media
                        .Where(m => !profilePhotoIds.Contains(m.MediaId))
                        .ToListAsync();

                    foreach (var media in mediaToDelete)
                    {
                        // Delete physical file if it exists
                        if (!string.IsNullOrEmpty(media.CoverPath) && File.Exists(media.CoverPath))
                        {
                            try
                            {
                                File.Delete(media.CoverPath);
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Failed to delete media file",
                                    $"Could not delete file: {media.CoverPath}",
                                    ex,
                                    false);
                            }
                        }
                    }

                    // Remove media records from database
                    context.Media.RemoveRange(mediaToDelete);

                    // 6. Clear directory configuration
                    await context.Directories.ExecuteDeleteAsync();

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Database cleanup completed",
                        $"Cleaned {mediaToDelete.Count} media files. Profiles and settings preserved.",
                        null,
                        false);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Database cleanup failed",
                        "Failed to clean library data. Database has been rolled back to previous state.",
                        ex,
                        true);
                    throw;
                }
            }, "Cleaning music database", ErrorSeverity.Critical, false);
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

                var profile = await _profileManager.GetCurrentProfileAsync();

                await _profileConfigService.UpdatePlaybackSettings(profile.ProfileID, value);

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
                var profile = await _profileManager.GetCurrentProfileAsync();
                await _profileConfigService.UpdateProfileTheme(profile.ProfileID, themeConfig);

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

                var profile = await _profileManager.GetCurrentProfileAsync();
                var profileConfig = await _profileConfigService.GetProfileConfig(profile.ProfileID);
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
                    await _profileConfigService.UpdateProfileTheme(profile.ProfileID, themeConfig);

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