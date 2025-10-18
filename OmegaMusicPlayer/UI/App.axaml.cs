using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.PresetTheme;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Models;
using OmegaMusicPlayer.Core.Navigation.Services;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Configuration.ViewModels;
using OmegaMusicPlayer.Features.Configuration.Views;
using OmegaMusicPlayer.Features.Home.ViewModels;
using OmegaMusicPlayer.Features.Home.Views;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Library.ViewModels;
using OmegaMusicPlayer.Features.Library.Views;
using OmegaMusicPlayer.Features.Playback.Services;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Features.Playback.Views;
using OmegaMusicPlayer.Features.Playlists.Services;
using OmegaMusicPlayer.Features.Playlists.ViewModels;
using OmegaMusicPlayer.Features.Playlists.Views;
using OmegaMusicPlayer.Features.Profile.Services;
using OmegaMusicPlayer.Features.Profile.ViewModels;
using OmegaMusicPlayer.Features.Profile.Views;
using OmegaMusicPlayer.Features.Search.Services;
using OmegaMusicPlayer.Features.Search.ViewModels;
using OmegaMusicPlayer.Features.Search.Views;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using OmegaMusicPlayer.Features.Shell.Views;
using OmegaMusicPlayer.Infrastructure.API;
using OmegaMusicPlayer.Infrastructure.Data;
using OmegaMusicPlayer.Infrastructure.Data.Repositories;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Library;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Profile;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Cache;
using OmegaMusicPlayer.Infrastructure.Services.Database;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using OmegaMusicPlayer.Infrastructure.Services.Initialization;
using OmegaMusicPlayer.UI.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.UI
{
    public partial class App : Application
    {
        private static Mutex _instanceMutex;
        private const string MUTEX_NAME = "Local\\OmegaMusicPlayer_Instance";

        public static IServiceProvider ServiceProvider { get; private set; }
        private bool _isFirstRun = false;
        private bool _isMutexSingleInstanceRun = false;

        private EmbeddedPostgreSqlService _embeddedPostgreSqlService;
        private DatabaseInitializationService _databaseInitializationService;
        private DatabaseErrorHandlingService _databaseErrorHandler;

        public override void Initialize()
        {
            // Check for single instance FIRST, before any other initialization
            _isMutexSingleInstanceRun = CheckSingleInstance();

            // Register global exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Configure pre-database services (for the Localize markup extension)
            var serviceCollection = new ServiceCollection();
            ConfigurePreDatabaseServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Skip if Omega Music Player is already running (error window will be displayed soon)
            if (_isMutexSingleInstanceRun)
            {
                // Initialize database services
                _embeddedPostgreSqlService = new EmbeddedPostgreSqlService();
                _databaseInitializationService = new DatabaseInitializationService(_embeddedPostgreSqlService);
                _databaseErrorHandler = _embeddedPostgreSqlService.ErrorHandler;
            }

            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Checks if another instance of OmegaMusicPlayer is already running
        /// </summary>
        /// <returns>True if this is the first instance, False if another instance is running</returns>
        private static bool CheckSingleInstance()
        {
            bool createdNew;
            try
            {
                // Create a named mutex that's accessible across all user sessions
                _instanceMutex = new Mutex(true, MUTEX_NAME, out createdNew);

                if (!createdNew)
                {
                    // Another instance is already running
                    _instanceMutex?.Dispose();
                    _instanceMutex = null;
                    return false;
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex exists but we don't have access
                // This means another instance is running
                return false;
            }
            catch (Exception)
            {
                // Any other error, assume we can continue
                return true;
            }
        }

        /// <summary>
        /// Releases the single instance mutex
        /// </summary>
        private static void ReleaseSingleInstance()
        {
            try
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
                _instanceMutex = null;
            }
            catch (Exception)
            {
                // Ignore errors during cleanup
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Remove Avalonia data validation
                BindingPlugins.DataValidators.RemoveAt(0);

                // Try to create media directories first
                try
                {
                    CreateMediaDirectories();
                }
                catch (Exception ex)
                {
                    // Media directories creation failed - show error window
                    var localizationService = ServiceProvider.GetRequiredService<LocalizationService>();
                    ErrorWindow errorWindow = new ErrorWindow();
                    errorWindow.ExitRequested += errorWindow_ExitRequested;
                    desktop.MainWindow = errorWindow;

                    errorWindow.ShowInitializationError(
                        localizationService["MediaDirectory_CreationFailed_Title"],
                        localizationService["MediaDirectory_CreationFailed_Message"],
                        ex.ToString());
                    return;
                }

                dynamic result = null;

                // if this is NOT the only instance of Omega Music Player skip database operation and show error window
                if (_isMutexSingleInstanceRun)
                {
                    // Try to initialize database synchronously
                    result = _databaseInitializationService.InitializeDatabase();
                }

                if (result != null && result.Success)
                {
                    // Dispose the previous ServiceProvider to prevent memory leaks
                    if (ServiceProvider is IDisposable disposableProvider)
                    {
                        disposableProvider.Dispose();
                    }

                    // Database ready - configure services
                    var serviceCollection = new ServiceCollection();
                    ConfigurePreDatabaseServices(serviceCollection);
                    ConfigureDatabaseDependentServices(serviceCollection);
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

                    // Register the ViewLocator
                    DataTemplates.Add(new ViewLocator());

                    // Initialize application services
                    InitializeApplicationServices();
                    CheckIfFirstRun();

                    desktop.MainWindow = ServiceProvider.GetRequiredService<MainView>();
                    desktop.MainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();

                    // Restore window state before showing the window
                    RestoreWindowState(desktop);

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
                                    var embeddedPostgres = ServiceProvider?.GetService<EmbeddedPostgreSqlService>();
                                    if (embeddedPostgres != null)
                                    {
                                        embeddedPostgres.StopServer();
                                        embeddedPostgres.Dispose();
                                    }

                                    var dbContext = ServiceProvider?.GetService<OmegaMusicPlayerDbContext>();
                                    dbContext?.Dispose();

                                    // Dispose media key service
                                    var mediaKeyService = ServiceProvider?.GetService<MediaKeyService>();
                                    mediaKeyService?.Dispose();

                                    // Release single instance mutex
                                    ReleaseSingleInstance();

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
                            var trackControlVM = ServiceProvider?.GetService<TrackControlViewModel>();
                            if (trackControlVM != null)
                            {
                                trackControlVM.StopPlayback();
                            }

                            var trackQueueVM = ServiceProvider?.GetService<TrackQueueViewModel>();
                            if (trackQueueVM != null)
                            {
                                await trackQueueVM.OnShutdown();
                            }

                            var embeddedPostgres = ServiceProvider?.GetService<EmbeddedPostgreSqlService>();
                            if (embeddedPostgres != null)
                            {
                                embeddedPostgres.StopServer();
                                embeddedPostgres.Dispose();
                            }

                            var dbContext = ServiceProvider?.GetService<OmegaMusicPlayerDbContext>();
                            dbContext?.Dispose();

                            // Dispose media key service
                            var mediaKeyService = ServiceProvider?.GetService<MediaKeyService>();
                            mediaKeyService?.Dispose();

                            // Release single instance mutex
                            ReleaseSingleInstance();
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
                else
                {
                    // Database failed - show error window
                    ErrorWindow errorWindow = new ErrorWindow();
                    errorWindow.ExitRequested += errorWindow_ExitRequested;
                    desktop.MainWindow = errorWindow;
                    if (!_isMutexSingleInstanceRun)
                    {
                        // Show Omegaa Player already running error immediately
                        var localizationService = ServiceProvider.GetRequiredService<LocalizationService>();
                        
                        errorWindow.ShowInitializationError(
                            localizationService["OmegaMusicPlayerAlreadyRunning_Title"],
                            localizationService["OmegaMusicPlayerAlreadyRunning_Message"],
                            localizationService["Troubleshoot_OmegaMusicPlayerAlreadyRunningMessage"]);
                    }
                    else
                    {
                        // Show the error immediately
                        errorWindow.ShowDatabaseError(result.Error, result.Phase);
                        LogDatabaseError(result.Error, result.Phase);
                        SaveDiagnosticReport(result.Error, result.Phase);
                    }
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Handle exit request from ErrorWindow
        /// </summary>
        private void errorWindow_ExitRequested(object sender, EventArgs e)
        {
            // Release single instance mutex before shutdown
            ReleaseSingleInstance();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(1);
            }
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
                        "Will set up profile.",
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

        /// <summary>
        /// Restores window state from global configuration
        /// </summary>
        private async void RestoreWindowState(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var globalConfigService = ServiceProvider.GetRequiredService<GlobalConfigurationService>();
                var (width, height, x, y, isMaximized) = await globalConfigService.GetWindowState();

                if (desktop.MainWindow != null)
                {
                    // Set window size with minimum constraints
                    desktop.MainWindow.MinWidth = 955;
                    desktop.MainWindow.MinHeight = 650;
                    desktop.MainWindow.Width = Math.Max(width, 955);
                    desktop.MainWindow.Height = Math.Max(height, 650);

                    // Set window state first
                    if (isMaximized)
                    {
                        desktop.MainWindow.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        desktop.MainWindow.WindowState = WindowState.Normal;

                        // Set position only if not maximized and position is valid
                        if (x.HasValue && y.HasValue)
                        {
                            desktop.MainWindow.Position = new PixelPoint(x.Value, y.Value);
                        }
                        else
                        {
                            desktop.MainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorHandlingService = ServiceProvider?.GetService<IErrorHandlingService>();
                errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to restore window state",
                    "Using default window size and position.",
                    ex,
                    false);

                // Apply defaults if restoration fails
                if (desktop.MainWindow != null)
                {
                    desktop.MainWindow.MinWidth = 955;
                    desktop.MainWindow.MinHeight = 650;
                    desktop.MainWindow.Width = 1450;
                    desktop.MainWindow.Height = 760;
                    desktop.MainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
        }

        /// <summary>
        /// Configures services that don't depend on the database
        /// </summary>
        private void ConfigurePreDatabaseServices(IServiceCollection services)
        {
            // Register core services that don't need database
            services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
            services.AddSingleton<LanguageDetectionService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ThemeService>(provider => new ThemeService(this));
            services.AddSingleton<MediaKeyService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<ToastNotificationService>();
            services.AddSingleton<MemoryMonitorService>();
            services.AddSingleton<ImageCacheService>();
            services.AddSingleton<ImageLoadingService>();
            services.AddSingleton<StandardImageService>();
            services.AddSingleton<SearchInputCleaner>();
            services.AddSingleton<SleepTimerManager>();
            services.AddSingleton<AudioMonitorService>();
            services.AddSingleton<INavigationService, NavigationService>();
        }

        /// <summary>
        /// Configures services that depend on the database
        /// </summary>
        private void ConfigureDatabaseDependentServices(IServiceCollection services)
        {
            // Register database services
            services.AddSingleton<EmbeddedPostgreSqlService>(_embeddedPostgreSqlService);
            services.AddSingleton<DatabaseInitializationService>(_databaseInitializationService);
            services.AddSingleton<DatabaseErrorHandlingService>(_databaseErrorHandler);

            // Database context factory
            services.AddDbContextFactory<OmegaMusicPlayerDbContext>((serviceProvider, options) =>
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

            // Register database-dependent services
            services.AddSingleton<GlobalConfigurationService>();
            services.AddSingleton<ProfileConfigurationService>();
            services.AddSingleton<TracksService>();
            services.AddSingleton<DirectoriesService>();
            services.AddSingleton<DirectoryScannerService>();
            services.AddSingleton<FileSystemWatcherService>();
            services.AddSingleton<AlbumService>();
            services.AddSingleton<ArtistsService>();
            services.AddSingleton<GenresService>();
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
            services.AddSingleton<QueueSaveCoordinator>();
            services.AddSingleton<ArtistDisplayService>();
            services.AddSingleton<AlbumDisplayService>();
            services.AddSingleton<GenreDisplayService>();
            services.AddSingleton<FolderDisplayService>();
            services.AddSingleton<PlaylistDisplayService>();
            services.AddSingleton<TrackSortService>();
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<StateManagerService>();
            services.AddSingleton<SearchService>();
            services.AddSingleton<PlayHistoryService>();
            services.AddSingleton<TrackStatsService>();
            services.AddSingleton<DeezerService>();
            services.AddSingleton<ErrorRecoveryService>(provider => new ErrorRecoveryService(
                provider,
                provider.GetRequiredService<IErrorHandlingService>(),
                provider.GetRequiredService<IDbContextFactory<OmegaMusicPlayerDbContext>>(),
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
            try
            {
                // Use the centralized configuration to create all necessary directories
                AppConfiguration.EnsureDirectoriesExist();
            }
            catch (Exception ex)
            {
                // Add build configuration info to error message for debugging
                throw new InvalidOperationException(
                    $"Failed to create application directories for {AppConfiguration.BuildConfiguration} build. " +
                    $"Path: {AppConfiguration.ApplicationDataPath}", ex);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogUnhandledException(exception, "Unhandled AppDomain exception", e.IsTerminating);

            // Release mutex if terminating
            if (e.IsTerminating)
            {
                ReleaseSingleInstance();
            }
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
                    // Fallback logging using centralized logs path
                    var logDir = AppConfiguration.LogsPath;
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, $"unhandled-error-{AppConfiguration.BuildConfiguration.ToLower()}-{DateTime.Now:yyyy-MM-dd-HHmmss}.log");

                    var logContent = $"Build Configuration: {AppConfiguration.BuildConfiguration}\n" +
                                   $"Database Path: {AppConfiguration.DatabasePath}\n" +
                                   $"{source}: {exception?.Message}\n{exception?.StackTrace}";

                    File.WriteAllText(logPath, logContent);
                }
            }
            catch { }
        }

        /// <summary>
        /// Log database errors for diagnostic purposes
        /// </summary>
        private void LogDatabaseError(DatabaseErrorHandlingService.DatabaseError error, string phase)
        {
            try
            {
                // Use centralized logs path
                var logDir = AppConfiguration.LogsPath;
                Directory.CreateDirectory(logDir);

                var logFile = Path.Combine(logDir, $"database-error-{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Database Error ({AppConfiguration.BuildConfiguration})\n" +
                              $"Phase: {phase}\n" +
                              $"Category: {error.Category}\n" +
                              $"Title: {error.UserFriendlyTitle}\n" +
                              $"Message: {error.UserFriendlyMessage}\n" +
                              $"Technical Details: {error.TechnicalDetails}\n" +
                              $"Is Recoverable: {error.IsRecoverable}\n" +
                              $"Database Path: {AppConfiguration.DatabasePath}\n" +
                              "---\n\n";

                File.AppendAllText(logFile, logEntry);
            }
            catch { }
        }

        /// <summary>
        /// Save diagnostic report for support
        /// </summary>
        private void SaveDiagnosticReport(DatabaseErrorHandlingService.DatabaseError error, string phase)
        {
            try
            {
                var diagnosticReport = _databaseErrorHandler.CreateDiagnosticReport(error, AppConfiguration.DatabasePath);
                var reportFile = Path.Combine(AppConfiguration.LogsPath,
                    $"diagnostic-report-{AppConfiguration.BuildConfiguration.ToLower()}-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt");

                Directory.CreateDirectory(AppConfiguration.LogsPath);

                // Add configuration info to the diagnostic report
                var fullReport = $"=== Build Configuration ===\n" +
                                AppConfiguration.GetDiagnosticInfo() + "\n\n" +
                                diagnosticReport;

                File.WriteAllText(reportFile, fullReport);
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
                    return "Enhanced database service not available";
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
                       $"Albums: {dbInfo.AlbumCount}\n" +
                       $"Error: {(string.IsNullOrEmpty(dbInfo.ErrorMessage) ? "None" : dbInfo.ErrorMessage)}";
            }
            catch (Exception ex)
            {
                return $"Error getting enhanced database info: {ex.Message}";
            }
        }
    }
}