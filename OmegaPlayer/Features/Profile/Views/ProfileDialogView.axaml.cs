using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Profile.Views
{
    public partial class ProfileDialogView : Window
    {
        public ProfileDialogView()
        {
            InitializeComponent();
            var profileService = App.ServiceProvider.GetService<ProfileService>();
            var messenger = App.ServiceProvider.GetService<IMessenger>();
            DataContext = new ProfileDialogViewModel(this, profileService, messenger);

        }
    }
}