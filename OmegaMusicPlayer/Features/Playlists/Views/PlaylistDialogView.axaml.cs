using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Playlists.Models;
using OmegaMusicPlayer.Features.Playlists.Services;
using OmegaMusicPlayer.Features.Playlists.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.UI;

namespace OmegaMusicPlayer.Features.Playlists.Views
{
    public partial class PlaylistDialogView : Window
    {
        public PlaylistDialogView()
        {
            InitializeComponent();

        }

        public void Initialize(Playlist playlistToEdit = null)
        {
            var playlistService = App.ServiceProvider.GetRequiredService<PlaylistService>();
            var profileManager = App.ServiceProvider.GetRequiredService<ProfileManager>();
            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            var errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();
            var messenger = App.ServiceProvider.GetRequiredService<IMessenger>();

            DataContext = new PlaylistDialogViewModel(
                this,
                playlistService,
                profileManager,
                localizationService,
                errorHandlingService,
                messenger,
                playlistToEdit);
        }
    }
}