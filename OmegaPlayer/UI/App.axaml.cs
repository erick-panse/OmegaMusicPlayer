using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.PresetTheme;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Navigation.Services;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Configuration.Views;
using OmegaPlayer.Features.Home.ViewModels;
using OmegaPlayer.Features.Home.Views;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Views;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playback.Views;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Profile.Views;
using OmegaPlayer.Features.Search.Services;
using OmegaPlayer.Features.Search.ViewModels;
using OmegaPlayer.Features.Search.Views;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Infrastructure.API;
using OmegaPlayer.Infrastructure.Data;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Infrastructure.Services.Database;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Infrastructure.Services.Initialization;
using OmegaPlayer.UI.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OmegaPlayer.UI
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        private bool _isFirstRun = false;
        private EmbeddedPostgreSqlService _embeddedPostgreSqlService;
        private DatabaseInitializationService _databaseInitializationService;

        public override void Initialize()
        {
            // Register global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Initialize database services and start PostgreSQL synchronously
            InitializeDatabaseServices();

            var databaseReady = StartDatabaseServerSync();
            if (!databaseReady)
            {
                throw new InvalidOperationException(
                    "Failed to start embedded PostgreSQL server. " +
                    "Please check available disk space and permissions.");
            }

            // Create necessary media directories
            CreateMediaDirectories();

            // Set up the Dependency Injection container - database is now ready
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
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

        /// <summary>
        /// Initialize database services early
        /// </summary>
        private void InitializeDatabaseServices()
        {
            _embeddedPostgreSqlService = new EmbeddedPostgreSqlService();
            _databaseInitializationService = new DatabaseInitializationService(_embeddedPostgreSqlService);
        }

        /// <summary>
        /// Start database server synchronously during app initialization
        /// </summary>
        private bool StartDatabaseServerSync()
        {
            try
            {
                // Start PostgreSQL server synchronously
                var serverStarted = _embeddedPostgreSqlService.StartServer();
                if (serverStarted)
                {
                    // Initialize database schema synchronously
                    var databaseReady = _databaseInitializationService.InitializeDatabase();
                    return databaseReady;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting database: {ex.Message}");
                return false;
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Remove Avalonia data validation
                BindingPlugins.DataValidators.RemoveAt(0);

                // Register the ViewLocator
                DataTemplates.Add(new ViewLocator());

                // Initialize application services synchronously
                InitializeApplicationServices();
                CheckIfFirstRun();

                // Create main window
                desktop.MainWindow = ServiceProvider.GetRequiredService<MainView>();
                desktop.MainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
                
                // Start media key listening
                var mediaKeyService = ServiceProvider.GetRequiredService<MediaKeyService>();
                mediaKeyService.StartListening();

                // Handle main window loaded event for first run setup
                desktop.MainWindow.Loaded += async (s, e) =>
                {
                    try
                    {
                        if (_isFirstRun)
                        {
                            var setupWindow = new SetupView();
                            var setupResult = await setupWindow.ShowDialog<bool?>(desktop.MainWindow);

                            if (setupResult != true)
                            {
                                desktop.Shutdown(0);
                                return;
                            }
                        }

                        var maintenanceService = ServiceProvider.GetRequiredService<LibraryMaintenanceService>();
                        await maintenanceService.PerformLibraryMaintenance(); 

                        var fileWatcher = ServiceProvider.GetRequiredService<FileSystemWatcherService>();
                        await fileWatcher.StartWatching();

                    }
                    catch (Exception ex)
                    {
                        var errorService = ServiceProvider?.GetService<IErrorHandlingService>();
                        errorService?.LogError(
                            ErrorSeverity.Critical,
                            "Application startup failed",
                            "A critical error occurred during application startup.",
                            ex,
                            true);
                    }
                };

                // Cleanup on shutdown
                desktop.ShutdownRequested += async (s, e) =>
                {
                    try
                    {
                        var embeddedPostgres = ServiceProvider?.GetService<EmbeddedPostgreSqlService>();
                        if (embeddedPostgres != null)
                        {
                            embeddedPostgres.StopServer();
                            embeddedPostgres.Dispose();
                        }

                        var dbContext = ServiceProvider?.GetService<OmegaPlayerDbContext>();
                        dbContext?.Dispose();

                        // Dispose media key service
                        var mediaKeyService = ServiceProvider?.GetService<MediaKeyService>();
                        mediaKeyService?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        var errorHandlingService = ServiceProvider?.GetService<IErrorHandlingService>();
                        errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Database cleanup error",
                            "Error during database cleanup on shutdown",
                            ex,
                            false);
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Checks if this is a first run by looking for existing profiles
        /// </summary>
        private async void CheckIfFirstRun()
        {
            try
            {
                var profileService = ServiceProvider.GetRequiredService<ProfileService>();
                var profiles = await profileService.GetAllProfiles();

                _isFirstRun = profiles == null || profiles.Count == 0 ||
                             (profiles.Count == 1 && profiles[0].ProfileName == "Default");

                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                if (_isFirstRun)
                {
                    errorHandlingService?.LogError(
                        ErrorSeverity.Info,
                        "First run detected",
                        "Welcome to Omega Player! We'll help you set up your profile.",
                        null,
                        false);
                }
            }
            catch (Exception ex)
            {
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error checking first run status",
                    "Could not determine if this is a first run. Assuming it is not.",
                    ex,
                    false);

                _isFirstRun = false;
            }
        }

        /// <summary>
        /// Initializes application services synchronously
        /// </summary>
        private void InitializeApplicationServices()
        {
            try
            {
                var themeService = ServiceProvider.GetRequiredService<ThemeService>();
                var profileManager = ServiceProvider.GetRequiredService<ProfileManager>();
                var profileConfigService = ServiceProvider.GetRequiredService<ProfileConfigurationService>();
                var stateManager = ServiceProvider.GetRequiredService<StateManagerService>();
                var localizationService = ServiceProvider.GetRequiredService<LocalizationService>();
                var globalConfigService = ServiceProvider.GetRequiredService<GlobalConfigurationService>();

                InitializeTheme(themeService, profileManager, profileConfigService);
                InitializeLanguage(localizationService, globalConfigService);
                stateManager.LoadAndApplyState();
            }
            catch (Exception ex)
            {
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error initializing application services",
                    "Some application settings could not be loaded. Default settings will be used.",
                    ex,
                    true);
            }
        }

        private async void InitializeLanguage(LocalizationService localizationService, GlobalConfigurationService globalConfigService)
        {
            try
            {
                var globalConfig = await globalConfigService.GetGlobalConfig();
                localizationService.ChangeLanguage(globalConfig.LanguagePreference);
            }
            catch (Exception ex)
            {
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error initializing language",
                    "Could not restore language settings. Using default language.",
                    ex,
                    true);
            }
        }

        private async void InitializeTheme(ThemeService themeService, ProfileManager profileManager, ProfileConfigurationService profileConfigService)
        {
            try
            {
                var profile = await profileManager.GetCurrentProfileAsync();
                var profileConfig = await profileConfigService.GetProfileConfig(profile.ProfileID);
                var themeConfig = ThemeConfiguration.FromJson(profileConfig.Theme);

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
                var errorHandlingService = ServiceProvider.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error initializing theme",
                    "Could not restore theme settings. Using default theme.",
                    ex,
                    true);
                themeService.ApplyPresetTheme(PresetTheme.Neon);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register database services
            services.AddSingleton<EmbeddedPostgreSqlService>(_embeddedPostgreSqlService);
            services.AddSingleton<DatabaseInitializationService>(_databaseInitializationService);

            services.AddDbContextFactory<OmegaPlayerDbContext>((serviceProvider, options) =>
            {
                var embeddedPostgres = serviceProvider.GetRequiredService<EmbeddedPostgreSqlService>();

                if (embeddedPostgres.IsServerRunning)
                {
                    options.UseNpgsql(embeddedPostgres.ConnectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.CommandTimeout(30);
                    });
                }
                else
                {
                    throw new InvalidOperationException("PostgreSQL server is not running when configuring DbContext");
                }

                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Register repositories
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

            // Register services
            services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
            services.AddSingleton<LanguageDetectionService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<GlobalConfigurationService>();
            services.AddSingleton<ProfileConfigurationService>();
            services.AddSingleton<MediaKeyService>();
            services.AddSingleton<TracksService>();
            services.AddSingleton<DirectoriesService>();
            services.AddSingleton<DirectoryScannerService>(); 
            services.AddSingleton<FileSystemWatcherService>();
            services.AddSingleton<AlbumService>();
            services.AddSingleton<ArtistsService>();
            services.AddSingleton<GenresService>();
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
            services.AddSingleton<LibraryMaintenanceService>();
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
            services.AddSingleton<SearchInputCleaner>();
            services.AddSingleton<SearchService>();
            services.AddSingleton<PlayHistoryService>();
            services.AddSingleton<TrackStatsService>();
            services.AddSingleton<DeezerService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<ToastNotificationService>();
            services.AddSingleton<ErrorRecoveryService>(provider => new ErrorRecoveryService(
                provider,
                provider.GetRequiredService<IErrorHandlingService>(),
                provider.GetRequiredService<IDbContextFactory<OmegaPlayerDbContext>>(),
                provider.GetRequiredService<EmbeddedPostgreSqlService>(),
                provider.GetRequiredService<IMessenger>()
                ));

            // Register ViewModels
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
            services.AddSingleton<LyricsViewModel>();
            services.AddSingleton<ImageModeViewModel>();
            services.AddSingleton<MainViewModel>();

            // Register Views
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
            services.AddTransient<LyricsView>();
            services.AddTransient<ImageModeView>();
            services.AddTransient<MainView>();
        }

        private void CreateMediaDirectories()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mediaDir = Path.Combine(baseDir, "media");

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
                var errorHandlingService = ServiceProvider?.GetService<IErrorHandlingService>();
                var localizationService = ServiceProvider?.GetService<LocalizationService>();
                var recoveryService = ServiceProvider?.GetService<ErrorRecoveryService>();

                if (errorHandlingService != null)
                {
                    var severity = isTerminating ? ErrorSeverity.Critical : ErrorSeverity.NonCritical;

                    // Try to use localized messages if available, fallback to English
                    var message = localizationService != null ?
                        localizationService[isTerminating ? "UnhandledExceptionTerminating" : "UnhandledExceptionHandled"] :
                        isTerminating ? "Critical Application Error" : "Unexpected Error";

                    var details = localizationService != null ?
                        localizationService[isTerminating ? "UnhandledExceptionTerminatingDetails" : "UnhandledExceptionHandledDetails"] :
                        isTerminating ?
                            "A critical error occurred that may cause the application to terminate." :
                            "An unexpected error occurred that was handled automatically.";

                    errorHandlingService.LogError(severity, message, details, exception, true);

                    if (isTerminating && recoveryService != null)
                    {
                        recoveryService.CreateEmergencyBackupAsync().GetAwaiter().GetResult();
                    }
                }
                else
                {
                    var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, $"unhandled-error-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
                    File.WriteAllText(logPath, $"{source}: {exception?.Message}\n{exception?.StackTrace}");
                }
            }
            catch { }
        }

        public string GetDatabaseInfo()
        {
            try
            {
                var dbInitService = ServiceProvider?.GetService<DatabaseInitializationService>();
                if (dbInitService == null)
                {
                    return "Database service not available";
                }

                var dbInfo = dbInitService.GetDatabaseInfo();

                return $"Database Type: PostgreSQL (Embedded)\n" +
                       $"Status: {dbInfo.Status}\n" +
                       $"Port: {dbInfo.Port}\n" +
                       $"Size: {dbInfo.SizeFormatted}\n" +
                       $"Profiles: {dbInfo.ProfileCount}\n" +
                       $"Tracks: {dbInfo.TrackCount}\n" +
                       $"Playlists: {dbInfo.PlaylistCount}\n" +
                       $"Artists: {dbInfo.ArtistCount}\n" +
                       $"Albums: {dbInfo.AlbumCount}";
            }
            catch (Exception ex)
            {
                return $"Error getting database info: {ex.Message}";
            }
        }
    }
}