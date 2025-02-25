using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playlists.ViewModels
{
    public partial class PlaylistSelectionDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly IEnumerable<TrackDisplayModel> _selectedTracks;
        private readonly PlaylistDisplayService _playlistDisplayService;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _playlistsList;

        public PlaylistSelectionDialogViewModel(
            Window dialog,
            PlaylistsViewModel playlistViewModel,
            IEnumerable<TrackDisplayModel> selectedTracks,
            ObservableCollection<PlaylistDisplayModel> playlists,
            PlaylistDisplayService playlistDisplayService)
        {
            _dialog = dialog;
            _playlistViewModel = playlistViewModel;
            _selectedTracks = selectedTracks;
            _playlistDisplayService = playlistDisplayService;

            // filter to remove favorite from the list shown
            var filteredList = playlists.Where(p => !p.IsFavoritePlaylist).ToList();
            _playlistsList = new ObservableCollection<PlaylistDisplayModel>(filteredList);
        }

        [RelayCommand]
        private async Task CreateNewPlaylist()
        {
            var playlistDialog = new PlaylistDialogView();
            playlistDialog.Initialize();

            // Store the current playlists count to compare after
            var allPlaylistsBefore = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();

            await playlistDialog.ShowDialog(_dialog);

            // Get all playlists after creation
            var allPlaylistsAfter = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();

            // Find the newly created playlist
            var newPlaylist = allPlaylistsAfter.FirstOrDefault(after =>
                !allPlaylistsBefore.Any(before => before.PlaylistID == after.PlaylistID));

            if (newPlaylist != null)
            {
                await _playlistViewModel.LoadPlaylists(); // Refresh playlists
                PlaylistsList = _playlistViewModel.Playlists;

                // Add the selected tracks to the new playlist using LibraryViewModel
                if (_selectedTracks?.Any() == true)
                {
                    await _playlistViewModel.AddTracksToPlaylist(newPlaylist.PlaylistID, _selectedTracks);
                }

                Close();

                // Navigate to the new playlist
                await _playlistViewModel.OpenPlaylistDetails(newPlaylist);
            }
        }

        [RelayCommand]
        private async Task AddToPlaylist(int playlistId)
        {
            if (_selectedTracks?.Any() == true)
            {
                await _playlistViewModel.AddTracksToPlaylist(playlistId, _selectedTracks);
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