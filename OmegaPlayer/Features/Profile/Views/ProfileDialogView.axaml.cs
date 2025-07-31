using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Profile.Views
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