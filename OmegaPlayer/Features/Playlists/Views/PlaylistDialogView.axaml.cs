using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Playlists.Views
{
    public partial class PlaylistDialogView : Window
    {
        public PlaylistDialogView()
        {
            InitializeComponent();
        }

        public void Initialize(int profileId, Playlist playlistToEdit = null)
        {
            var playlistService = App.ServiceProvider.GetService<PlaylistService>();
            DataContext = new PlaylistDialogViewModel(this, playlistService, profileId, playlistToEdit);
        }

    }
}