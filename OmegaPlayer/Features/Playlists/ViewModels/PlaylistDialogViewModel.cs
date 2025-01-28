using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playlists.ViewModels
{
    public partial class PlaylistDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly PlaylistService _playlistService;
        private readonly Playlist _playlistToEdit;
        private readonly int _profileId;

        [ObservableProperty]
        private string _playlistName;

        [ObservableProperty]
        private string _dialogTitle;

        [ObservableProperty]
        private string _saveButtonText;

        [ObservableProperty]
        private string _validationMessage;

        [ObservableProperty]
        private bool _showValidationMessage;

        public PlaylistDialogViewModel(
            Window dialog,
            PlaylistService playlistService,
            int profileId,
            Playlist playlistToEdit = null)
        {
            _dialog = dialog;
            _playlistService = playlistService;
            _profileId = profileId;
            _playlistToEdit = playlistToEdit;

            InitializeDialog();
        }

        private void InitializeDialog()
        {
            if (_playlistToEdit != null)
            {
                DialogTitle = "Edit Playlist";
                SaveButtonText = "Save";
                PlaylistName = _playlistToEdit.Title;
            }
            else
            {
                DialogTitle = "Create Playlist";
                SaveButtonText = "Create";
                PlaylistName = string.Empty;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(PlaylistName))
            {
                ShowValidationError("Playlist name cannot be empty");
                return;
            }

            try
            {
                var existingPlaylists = await _playlistService.GetAllPlaylists();
                bool nameExists = existingPlaylists.Exists(p =>
                    p.Title.Equals(PlaylistName, StringComparison.OrdinalIgnoreCase) &&
                    (_playlistToEdit == null || p.PlaylistID != _playlistToEdit.PlaylistID));

                if (_playlistToEdit != null)
                {
                    var updatedPlaylist = new Playlist
                    {
                        PlaylistID = _playlistToEdit.PlaylistID, // Keep the original ID
                        Title = PlaylistName,
                        ProfileID = _profileId,
                        CreatedAt = _playlistToEdit.CreatedAt, // Keep original creation date
                        UpdatedAt = DateTime.Now
                    };
                    await _playlistService.UpdatePlaylist(updatedPlaylist);
                }
                else
                {
                    var newPlaylist = new Playlist
                    {
                        Title = PlaylistName,
                        ProfileID = _profileId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    newPlaylist.PlaylistID = await _playlistService.AddPlaylist(newPlaylist);
                }
                Close();
            }
            catch (Exception ex)
            {
                ShowValidationError($"Error saving playlist: {ex.Message}");
            }
        }

        private void ShowValidationError(string message)
        {
            ValidationMessage = message;
            ShowValidationMessage = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            Close();
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }
    }
}