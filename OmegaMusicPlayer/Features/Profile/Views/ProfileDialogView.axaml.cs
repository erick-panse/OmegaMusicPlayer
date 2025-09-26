using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Profile.Services;
using OmegaMusicPlayer.Features.Profile.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using OmegaMusicPlayer.UI;

namespace OmegaMusicPlayer.Features.Profile.Views
{
    public partial class ProfileDialogView : Window
    {

        public ProfileDialogView()
        {
            InitializeComponent();

            // Get services
            var errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
            var profileService = App.ServiceProvider.GetService<ProfileService>();
            var profileManager = App.ServiceProvider.GetService<ProfileManager>();
            var localizationService = App.ServiceProvider.GetService<LocalizationService>();
            var standardImageService = App.ServiceProvider.GetService<StandardImageService>();
            var messenger = App.ServiceProvider.GetService<IMessenger>();

            DataContext = new ProfileDialogViewModel(
                this,
                profileService,
                profileManager,
                localizationService,
                standardImageService,
                messenger,
                errorHandlingService);
        }
    }
}