using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using OmegaPlayer.Services;
using OmegaPlayer.ViewModels;
using OmegaPlayer.Views;
using System;

namespace OmegaPlayer
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

            // Register the ViewModel
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<GridViewModel>();
            services.AddSingleton<ListViewModel>();
            services.AddSingleton<HomeViewModel>();

            // Register the View
            services.AddTransient<MainView>();
            services.AddTransient<GridView>();
            services.AddTransient<ListView>();
            services.AddTransient<HomeView>();
        }

    }
}