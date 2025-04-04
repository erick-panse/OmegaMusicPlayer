using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaPlayer.Features.Home.ViewModels;
using OmegaPlayer.Features.Home.Views;
using OmegaPlayer.Features.Configuration.Views;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Views;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Playback.Views;
using System.IO;
using OmegaPlayer.Core.Navigation.Services;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Profile.Views;
using OmegaPlayer.Core.Services;
using System.Threading.Tasks;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Features.Search.ViewModels;
using OmegaPlayer.Features.Search.Services;
using OmegaPlayer.Features.Search.Views;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.UI.Services;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.UI
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public override void Initialize()
        {
            // Register global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Set up the Dependency Injection container
            var serviceCollection = new ServiceCollection();

            // Register services and view models
            ConfigureServices(serviceCollection);

            // Create necessary media directories
            CreateMediaDirectories();

            // Build the service provider (DI container)
            ServiceProvider = serviceCollection.BuildServiceProvider();

            var messenger = ServiceProvider.GetRequiredService<IMessenger>();
            messenger.Register<ThemeUpdatedMessage>(this, (r, m) =>
            {
                var themeService = ServiceProvider.GetRequiredService<ThemeService>();
                if (m.NewTheme.ThemeType == PresetTheme.Custom)
                {
                    themeService.ApplyTheme(m.NewTheme.ToThemeColors());
                }
                else
                {
                    themeService.ApplyPresetTheme(m.NewTheme.ThemeType);
                }
            });

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                TryRecoverFromPreviousCrashAsync();

                var themeService = ServiceProvider.GetRequiredService<ThemeService>();
                var profileManager = ServiceProvider.GetRequiredService<ProfileManager>();
                var profileConfigService = ServiceProvider.GetRequiredService<ProfileConfigurationService>();
                var stateManager = ServiceProvider.GetRequiredService<StateManagerService>();
                var localizationService = ServiceProvider.GetRequiredService<LocalizationService>();
                var globalConfigService = ServiceProvider.GetRequiredService<GlobalConfigurationService>();

                // Initialize and apply theme and states first
                InitializeThemeAsync(themeService, profileManager, profileConfigService).ConfigureAwait(false);
                InitializeLanguageAsync(localizationService, globalConfigService).ConfigureAwait(false);
                stateManager.LoadAndApplyState().ConfigureAwait(false);

                // Line below is needed to remove Avalonia data validation.
                // Without this line you will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);

                // Register the ViewLocator using DI
                DataTemplates.Add(new ViewLocator());
                var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();

                mainViewModel.StartBackgroundScan();

                desktop.MainWindow = ServiceProvider.GetRequiredService<MainView>();
                desktop.MainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task InitializeLanguageAsync(LocalizationService localizationService, GlobalConfigurationService globalConfigService)
        {
            try
            {
                // Get global config
                var globalConfig = await globalConfigService.GetGlobalConfig();

                // Set current language or default
                localizationService.ChangeLanguage(globalConfig.LanguagePreference);
            }
            catch (Exception ex)
            {
                // Log error
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error initializing language",
                    "Could not restore application state from emergency backup.",
                    ex,
                    true);
            }
        }

        private async Task InitializeThemeAsync(ThemeService themeService, ProfileManager profileManager, ProfileConfigurationService profileConfigService)
        {
            try
            {
                // Ensure profile is initialized
                await profileManager.InitializeAsync();

                // Get current profile's config
                var profileConfig = await profileConfigService.GetProfileConfig(profileManager.CurrentProfile.ProfileID);

                // Parse theme configuration from profile config
                var themeConfig = ThemeConfiguration.FromJson(profileConfig.Theme);

                // Apply theme
                if (themeConfig.ThemeType == PresetTheme.Custom)
                {
                    themeService.ApplyTheme(themeConfig.ToThemeColors());
                }
                else
                {
                    themeService.ApplyPresetTheme(themeConfig.ThemeType);
                }
            }
            catch (Exception ex)
            {
                // Log error and apply default themevar errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error initializing theme",
                    "Could not restore application state from emergency backup.",
                    ex,
                    true);
                themeService.ApplyPresetTheme(PresetTheme.DarkNeon);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register all repositories here
            services.AddSingleton<GlobalConfigRepository>();
            services.AddSingleton<ProfileConfigRepository>();
            services.AddSingleton<TracksRepository>();
            services.AddSingleton<DirectoriesRepository>();
            services.AddSingleton<AlbumRepository>();
            services.AddSingleton<ArtistsRepository>();
            services.AddSingleton<GenresRepository>();
            services.AddSingleton<MediaRepository>();
            services.AddSingleton<PlaylistRepository>();
            services.AddSingleton<PlaylistTracksRepository>();
            services.AddSingleton<ProfileRepository>();
            services.AddSingleton<TrackArtistRepository>();
            services.AddSingleton<TrackDisplayRepository>();
            services.AddSingleton<TrackGenreRepository>();
            services.AddSingleton<CurrentQueueRepository>();
            services.AddSingleton<QueueTracksRepository>();
            services.AddSingleton<AllTracksRepository>();
            services.AddSingleton<PlayHistoryRepository>();
            services.AddSingleton<TrackStatsRepository>();

            // Register all services here
            services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<GlobalConfigurationService>();
            services.AddSingleton<ProfileConfigurationService>();
            services.AddSingleton<TracksService>();
            services.AddSingleton<DirectoriesService>();
            services.AddSingleton<DirectoryScannerService>();
            services.AddSingleton<AlbumService>();
            services.AddSingleton<ArtistsService>();
            services.AddSingleton<GenresService>();
            // Important: Register memory monitor service BEFORE image cache services
            services.AddSingleton<MemoryMonitorService>();
            services.AddSingleton<ImageCacheService>();
            services.AddSingleton<ImageLoadingService>();
            services.AddSingleton<StandardImageService>();
            services.AddSingleton<MediaService>();
            services.AddSingleton<PlaylistService>();
            services.AddSingleton<PlaylistTracksService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<TrackArtistService>();
            services.AddSingleton<TrackDisplayService>();
            services.AddSingleton<TrackGenreService>();
            services.AddSingleton<TrackMetadataService>();
            services.AddSingleton<QueueService>();
            services.AddSingleton<ArtistDisplayService>();
            services.AddSingleton<AlbumDisplayService>();
            services.AddSingleton<GenreDisplayService>();
            services.AddSingleton<FolderDisplayService>();
            services.AddSingleton<PlaylistDisplayService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<TrackSortService>();
            services.AddSingleton<SleepTimerManager>();
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<StateManagerService>();
            services.AddSingleton<ThemeService>(provider => new ThemeService(this));
            services.AddSingleton<AudioMonitorService>();
            services.AddSingleton<SearchService>();
            services.AddSingleton<PlayHistoryService>();
            services.AddSingleton<TrackStatsService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<ToastNotificationService>();
            services.AddSingleton<ErrorRecoveryService>();

            // Register the ViewModel here
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<DetailsViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<TrackQueueViewModel>();
            services.AddSingleton<TrackControlViewModel>();
            services.AddSingleton<ConfigViewModel>();
            services.AddSingleton<ArtistsViewModel>();
            services.AddSingleton<AlbumsViewModel>();
            services.AddSingleton<GenresViewModel>();
            services.AddSingleton<FoldersViewModel>();
            services.AddSingleton<PlaylistsViewModel>();
            services.AddSingleton<SleepTimerDialogViewModel>();
            services.AddSingleton<ProfileDialogViewModel>();
            services.AddSingleton<PlaylistDialogViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<MainViewModel>();


            // Register the View here
            services.AddTransient<LibraryView>();
            services.AddTransient<DetailsView>();
            services.AddTransient<HomeView>();
            services.AddTransient<TrackControlView>();
            services.AddTransient<ConfigView>();
            services.AddTransient<ArtistsView>();
            services.AddTransient<AlbumsView>();
            services.AddTransient<GenresView>();
            services.AddTransient<FoldersView>();
            services.AddTransient<PlaylistsView>();
            services.AddTransient<ProfileDialogView>();
            services.AddTransient<SleepTimerDialog>();
            services.AddTransient<PlaylistDialogView>();
            services.AddSingleton<SearchView>();
            services.AddTransient<MainView>();
        }

        private void CreateMediaDirectories()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mediaDir = Path.Combine(baseDir, "media");

            // Create the directories
            var directories = new[]
            {
                Path.Combine(mediaDir, "track_cover"),
                Path.Combine(mediaDir, "album_cover"),
                Path.Combine(mediaDir, "artist_photo")
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                        Console.WriteLine($"Created directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating directory {dir}: {ex.Message}");
                    }
                }
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogUnhandledException(exception, "Unhandled AppDomain exception", e.IsTerminating);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception, "Unobserved Task exception", false);
            e.SetObserved(); // Prevent application crash
        }

        private void LogUnhandledException(Exception exception, string source, bool isTerminating)
        {
            try
            {
                // Try to get error handling service if available
                var errorHandlingService = ServiceProvider?.GetService<IErrorHandlingService>();
                var recoveryService = ServiceProvider?.GetService<ErrorRecoveryService>();

                if (errorHandlingService != null)
                {
                    var severity = isTerminating ? ErrorSeverity.Critical : ErrorSeverity.NonCritical;

                    errorHandlingService.LogError(
                        severity,
                        $"Unhandled exception from {source}",
                        isTerminating ?
                            "A critical error occurred that may cause the application to terminate." :
                            "An unexpected error occurred that was handled automatically.",
                        exception,
                        true);

                    // For critical errors that will terminate the app, try to create an emergency backup
                    if (isTerminating && recoveryService != null)
                    {
                        // Run synchronously to ensure it completes before termination
                        recoveryService.CreateEmergencyBackupAsync().GetAwaiter().GetResult();
                    }
                }
                else
                {
                    // Fallback logging if service not available
                    var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, $"unhandled-error-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
                    File.WriteAllText(logPath, $"{source}: {exception?.Message}\n{exception?.StackTrace}");
                }
            }
            catch
            {
                // Last resort fallback - can't do much more
            }
        }
        // Add this method to App.axaml.cs to attempt recovery on startup
        private async Task TryRecoverFromPreviousCrashAsync()
        {
            try
            {
                var recoveryService = ServiceProvider.GetService<ErrorRecoveryService>();
                if (recoveryService != null)
                {
                    var restored = await recoveryService.TryRestoreFromBackupAsync();

                    if (restored)
                    {
                        var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                        errorHandlingService?.LogError(
                            ErrorSeverity.Info,
                            "Recovery from previous crash",
                            "The application was recovered from a previous crash using an emergency backup.",
                            null,
                            true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash during startup
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to recover from previous crash",
                    "Could not restore application state from emergency backup.",
                    ex,
                    true);
            }
        }

    }
}