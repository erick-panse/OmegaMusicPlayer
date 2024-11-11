using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.UserProfile.Services;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Infrastructure.Services.Config;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Data.Repositories.Core;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaPlayer.Infrastructure.Data.Repositories.UserProfile;
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

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
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

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register all your services here
            services.AddSingleton<TracksRepository>();
            services.AddSingleton<DirectoriesRepository>();
            services.AddSingleton<BlackListRepository>();
            services.AddSingleton<AlbumRepository>();
            services.AddSingleton<ArtistsRepository>();
            services.AddSingleton<BlackListProfileRepository>();
            services.AddSingleton<BlackListRepository>();
            services.AddSingleton<ConfigRepository>();
            services.AddSingleton<GenresRepository>();
            services.AddSingleton<LikeRepository>();
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

            // Register all your services here
            services.AddSingleton<TracksService>();
            services.AddSingleton<DirectoriesService>();
            services.AddSingleton<BlackListService>();
            services.AddSingleton<DirectoryScannerService>();
            services.AddSingleton<AlbumService>();
            services.AddSingleton<ArtistsService>();
            services.AddSingleton<BlackListProfileService>();
            services.AddSingleton<BlackListService>();
            services.AddSingleton<ConfigService>();
            services.AddSingleton<GenresService>();
            services.AddSingleton<ImageCacheService>();
            services.AddSingleton<LikeService>();
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

            // Register the ViewModel
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<ListViewModel>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<TrackQueueViewModel>();
            services.AddSingleton<TrackControlViewModel>();
            services.AddSingleton<ConfigViewModel>();

            // Register the View
            services.AddTransient<MainView>();
            services.AddTransient<LibraryView>();
            services.AddTransient<ListView>();
            services.AddTransient<HomeView>();
            services.AddTransient<TrackControlView>();
            services.AddTransient<ConfigView>();
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