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

namespace OmegaPlayer.UI
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public override void Initialize()
        {
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
                var themeService = ServiceProvider.GetRequiredService<ThemeService>();
                var profileManager = ServiceProvider.GetRequiredService<ProfileManager>();
                var profileConfigService = ServiceProvider.GetRequiredService<ProfileConfigurationService>();
                var stateManager = ServiceProvider.GetRequiredService<StateManagerService>();

                // Initialize and apply theme and states first
                InitializeThemeAsync(themeService, profileManager, profileConfigService).ConfigureAwait(false);
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
                // Log error and apply default theme
                Console.WriteLine($"Error initializing theme: {ex.Message}");
                themeService.ApplyPresetTheme(PresetTheme.Dark);
            }
        }


        private void ConfigureServices(IServiceCollection services)
        {
            // Register all repositories here
            services.AddSingleton<GlobalConfigRepository>();
            services.AddSingleton<ProfileConfigRepository>();
            services.AddSingleton<BlacklistedDirectoryRepository>();
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
            services.AddSingleton<UserActivityRepository>();
            services.AddSingleton<CurrentQueueRepository>();
            services.AddSingleton<QueueTracksRepository>();
            services.AddSingleton<AllTracksRepository>();

            // Register all services here
            services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
            services.AddSingleton<GlobalConfigurationService>();
            services.AddSingleton<ProfileConfigurationService>();
            services.AddSingleton<BlacklistedDirectoryService>();
            services.AddSingleton<TracksService>();
            services.AddSingleton<DirectoriesService>();
            services.AddSingleton<DirectoryScannerService>();
            services.AddSingleton<AlbumService>();
            services.AddSingleton<ArtistsService>();
            services.AddSingleton<GenresService>();
            services.AddSingleton<ImageCacheService>();
            services.AddSingleton<MediaService>();
            services.AddSingleton<PlaylistService>();
            services.AddSingleton<PlaylistTracksService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<TrackArtistService>();
            services.AddSingleton<TrackDisplayService>();
            services.AddSingleton<TrackGenreService>();
            services.AddSingleton<TrackMetadataService>();
            services.AddSingleton<UserActivityService>();
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

            // Register the ViewModel here
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<TrackQueueViewModel>();
            services.AddSingleton<TrackControlViewModel>();
            services.AddSingleton<ConfigViewModel>();
            services.AddSingleton<ArtistViewModel>();
            services.AddSingleton<AlbumViewModel>();
            services.AddSingleton<GenreViewModel>();
            services.AddSingleton<FolderViewModel>();
            services.AddSingleton<PlaylistViewModel>();
            services.AddSingleton<SleepTimerDialogViewModel>();
            services.AddSingleton<ProfileDialogViewModel>();
            services.AddSingleton<PlaylistDialogViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<MainViewModel>();


            // Register the View here
            services.AddTransient<LibraryView>();
            services.AddTransient<HomeView>();
            services.AddTransient<TrackControlView>();
            services.AddTransient<ConfigView>();
            services.AddTransient<ArtistView>();
            services.AddTransient<AlbumView>();
            services.AddTransient<GenreView>();
            services.AddTransient<FolderView>();
            services.AddTransient<PlaylistView>();
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

    }
}