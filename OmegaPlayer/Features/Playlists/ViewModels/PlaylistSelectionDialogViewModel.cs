using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Views;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playlists.ViewModels
{
    public partial class PlaylistSelectionDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly PlaylistViewModel _playlistViewModel;
        private readonly TrackDisplayModel _selectedTrack;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _playlistsList;

        public PlaylistSelectionDialogViewModel(
            Window dialog,
            PlaylistViewModel playlistViewModel,
            TrackDisplayModel selectedTrack,
            ObservableCollection<PlaylistDisplayModel> playlists)
        {
            _dialog = dialog;
            _playlistViewModel = playlistViewModel;
            _selectedTrack = selectedTrack;
            _playlistsList = playlists;
        }

        [RelayCommand]
        private async Task CreateNewPlaylist()
        {
            var playlistDialog = new PlaylistDialogView();
            playlistDialog.Initialize(2); // Using mock profile ID for now

            var result = await playlistDialog.ShowDialog<Playlist>(_dialog);
            if (result != null)
            {
                // Refresh playlists after creation
                await _playlistViewModel.LoadPlaylists();
                PlaylistsList = _playlistViewModel.Playlists;
            }
        }

        [RelayCommand]
        private async Task AddToPlaylist(int playlistId)
        {
            if (_selectedTrack != null)
            {
                await _playlistViewModel.AddTracksToPlaylist(playlistId, new[] { _selectedTrack });
            }
            Close();
        }

        [RelayCommand]
        private void Close()
        {
            _dialog?.Close();
        }
    }
}