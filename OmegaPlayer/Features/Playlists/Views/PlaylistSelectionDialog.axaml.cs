using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.UI;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Playlists.Views
{
    public partial class PlaylistSelectionDialog : Window
    {
        public PlaylistSelectionDialog()
        {
            InitializeComponent();
        }

        public void Initialize(
        PlaylistViewModel playlistViewModel,
        LibraryViewModel libraryViewModel,
        IEnumerable<TrackDisplayModel> selectedTracks)
        {
            var playlistDisplayService = App.ServiceProvider.GetService<PlaylistDisplayService>();
            DataContext = new PlaylistSelectionDialogViewModel(
                this,
                playlistViewModel,
                selectedTracks,
                playlistViewModel.Playlists,
                playlistDisplayService);
        }
    }
}