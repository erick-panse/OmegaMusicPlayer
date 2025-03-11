using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Playlists.Views
{
    public partial class PlaylistDialogView : Window
    {
        public PlaylistDialogView()
        {
            InitializeComponent();
        }

        public void Initialize(Playlist playlistToEdit = null)
        {
            var playlistService = App.ServiceProvider.GetService<PlaylistService>();
            var profileManager = App.ServiceProvider.GetService<ProfileManager>();
            var localizationService = App.ServiceProvider.GetService<LocalizationService>();
            DataContext = new PlaylistDialogViewModel(this, playlistService, profileManager, localizationService);
        }

    }
}