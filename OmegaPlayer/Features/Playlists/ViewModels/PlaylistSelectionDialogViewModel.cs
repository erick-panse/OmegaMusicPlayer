using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Infrastructure.Services;
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
        private readonly LocalizationService _localizationService;
        private readonly IEnumerable<TrackDisplayModel> _selectedTracks;
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _playlistsList;

        public PlaylistSelectionDialogViewModel(
            Window dialog,
            PlaylistsViewModel playlistViewModel,
            LocalizationService localizationService,
            IEnumerable<TrackDisplayModel> selectedTracks,
            PlaylistDisplayService playlistDisplayService,
            IErrorHandlingService errorHandlingService)
        {
            _dialog = dialog;
            _playlistViewModel = playlistViewModel;
            _localizationService = localizationService;
            _selectedTracks = selectedTracks;
            _playlistDisplayService = playlistDisplayService;
            _errorHandlingService = errorHandlingService;

            InitializePlaylistsList();
        }

        private void InitializePlaylistsList()
        {
            _errorHandlingService.SafeExecute(
                async () =>
                {
                    var playlists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();

                    // filter to remove favorite from the list shown
                    var filteredList = playlists?.Where(p => !p.IsFavoritePlaylist).ToList() ?? new List<PlaylistDisplayModel>();
                    PlaylistsList = new ObservableCollection<PlaylistDisplayModel>(filteredList);
                },
                "Initializing playlists list",
                ErrorSeverity.NonCritical,
                false
            );
        }

        [RelayCommand]
        private async Task CreateNewPlaylist()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                _localizationService["CreatePlaylistError"],
                ErrorSeverity.NonCritical,
                true
            );
        }

        [RelayCommand]
        private async Task AddToPlaylist(int playlistId)
        {
            if (playlistId <= 0)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid playlist ID",
                    $"Attempted to add tracks to an invalid playlist ID: {playlistId}",
                    null,
                    false
                );
                return;
            }

            if (_selectedTracks?.Any() == true)
            {
                await _playlistViewModel.AddTracksToPlaylist(playlistId, _selectedTracks);
            }
            else
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "No tracks selected",
                    "Attempted to add tracks to playlist but no tracks were selected",
                    null,
                    false
                );
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