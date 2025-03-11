using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.Features.Library.Services;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Configuration.Views
{
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();

            // Get all required services from DI
            var directoriesService = App.ServiceProvider.GetRequiredService<DirectoriesService>();
            var blacklistService = App.ServiceProvider.GetRequiredService<BlacklistedDirectoryService>();
            var profileManager = App.ServiceProvider.GetRequiredService<ProfileManager>();
            var profileConfigService = App.ServiceProvider.GetRequiredService<ProfileConfigurationService>();
            var globalConfigService = App.ServiceProvider.GetRequiredService<GlobalConfigurationService>();
            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            var messenger = App.ServiceProvider.GetRequiredService<IMessenger>();

            // Get StorageProvider from MainWindow using proper casting
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var storageProvider = mainWindow?.StorageProvider;

            DataContext = new ConfigViewModel(
                directoriesService,
                blacklistService,
                profileManager,
                profileConfigService,
                globalConfigService, 
                localizationService,
                messenger,
                storageProvider
            );
        }
    }
}