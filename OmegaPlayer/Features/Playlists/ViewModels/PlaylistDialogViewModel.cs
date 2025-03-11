using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playlists.ViewModels
{
    public partial class PlaylistDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly PlaylistService _playlistService;
        private readonly Playlist _playlistToEdit;
        private readonly ProfileManager _profileManager;
        private readonly LocalizationService _localizationService;

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
            ProfileManager profileManager,
            LocalizationService localizationService,
            Playlist playlistToEdit = null)
        {
            _dialog = dialog;
            _playlistService = playlistService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _playlistToEdit = playlistToEdit;

            InitializeDialog();
        }

        private void InitializeDialog()
        {
            if (_playlistToEdit != null)
            {
                DialogTitle = _localizationService["EditPlaylist"];
                SaveButtonText = _localizationService["Save"];
                PlaylistName = _playlistToEdit.Title;
            }
            else
            {
                DialogTitle = _localizationService["CreatePlaylist"];
                SaveButtonText = _localizationService["Create"];
                PlaylistName = string.Empty;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(PlaylistName))
            {
                ShowValidationError(_localizationService["PlaylistNameEmpty"]);
                return;
            }

            try
            {
                var existingPlaylists = await _playlistService.GetAllPlaylists();
                bool nameExists = existingPlaylists.Exists(p =>
                    p.Title.Equals(PlaylistName, StringComparison.OrdinalIgnoreCase) &&
                    (_playlistToEdit == null || p.PlaylistID != _playlistToEdit.PlaylistID));

                if (nameExists || PlaylistName == _localizationService["Favorites"])
                {
                    ShowValidationError(_localizationService["PlaylistNameExists"]);
                    return;
                }

                if (_playlistToEdit != null)
                {
                    var updatedPlaylist = new Playlist
                    {
                        PlaylistID = _playlistToEdit.PlaylistID, // Keep original
                        Title = PlaylistName,
                        ProfileID = _playlistToEdit.ProfileID, // Keep original 
                        CreatedAt = _playlistToEdit.CreatedAt, // Keep original 
                        UpdatedAt = DateTime.Now
                    };
                    await _playlistService.UpdatePlaylist(updatedPlaylist);
                }
                else
                {
                    var newPlaylist = new Playlist
                    {
                        Title = PlaylistName,
                        ProfileID = _profileManager.CurrentProfile.ProfileID,
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