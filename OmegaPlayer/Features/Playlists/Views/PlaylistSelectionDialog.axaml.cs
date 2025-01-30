using Avalonia.Controls;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.ViewModels;

namespace OmegaPlayer.Features.Playlists.Views
{
    public partial class PlaylistSelectionDialog : Window
    {
        public PlaylistSelectionDialog()
        {
            InitializeComponent();
        }

        public void Initialize(PlaylistViewModel playlistViewModel, TrackDisplayModel selectedTrack)
        {
            DataContext = new PlaylistSelectionDialogViewModel(
                this,
                playlistViewModel,
                selectedTrack,
                playlistViewModel.Playlists);
        }
    }
}