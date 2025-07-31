using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.ComponentModel;
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
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private string _playlistName;

        [ObservableProperty]
        private string _dialogTitle;

        [ObservableProperty]
        private string _saveButtonText;

        [ObservableProperty]
        private string _validationMessage = "";
        
        [ObservableProperty]
        private bool _hasValidationError = false;

        [ObservableProperty]
        private bool _isValidating = false;

        public PlaylistDialogViewModel(
            Window dialog,
            PlaylistService playlistService,
            ProfileManager profileManager,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService,
            Playlist playlistToEdit = null)
        {
            _dialog = dialog;
            _playlistService = playlistService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;
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

        private async Task ValidatePlaylistName()
        {
            if (IsValidating) return;

            IsValidating = true;

            try
            {
                if (string.IsNullOrWhiteSpace(PlaylistName))
                {
                    ValidationMessage = string.Empty;
                    HasValidationError = false;
                    return;
                }

                var excludeId = _playlistToEdit?.PlaylistID;
                var validationMessage = await _playlistService.ValidatePlaylistNameAsync(PlaylistName, excludeId);

                ValidationMessage = validationMessage ?? string.Empty;
                HasValidationError = !string.IsNullOrEmpty(validationMessage);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error validating playlist name",
                    ex.Message,
                    ex,
                    false);
            }
            finally
            {
                IsValidating = false;
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(PlaylistName))
            {
                // Debounce validation to avoid excessive calls
                _ = Task.Delay(300).ContinueWith(async _ => await ValidatePlaylistName());
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            // Basic validation to provide immediate feedback
            if (string.IsNullOrWhiteSpace(PlaylistName))
            {
                ValidationMessage = _localizationService["PlaylistNameEmpty"];
                HasValidationError = true;
                return;
            }

            // Validate before saving
            await ValidatePlaylistName();
            if (HasValidationError)
                return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    try
                    {
                        if (_playlistToEdit != null)
                        {
                            var updatedPlaylist = new Playlist
                            {
                                PlaylistID = _playlistToEdit.PlaylistID,
                                Title = PlaylistName.Trim(),
                                ProfileID = _playlistToEdit.ProfileID,
                                CreatedAt = _playlistToEdit.CreatedAt,
                                UpdatedAt = DateTime.UtcNow
                            };
                            await _playlistService.UpdatePlaylist(updatedPlaylist);
                        }
                        else
                        {
                            var profile = await _profileManager.GetCurrentProfileAsync();

                            var newPlaylist = new Playlist
                            {
                                Title = PlaylistName.Trim(),
                                ProfileID = profile.ProfileID,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            newPlaylist.PlaylistID = await _playlistService.AddPlaylist(newPlaylist);
                        }
                        Close();
                    }
                    catch (ArgumentException ex) when (ex.Message.Contains("name") || ex.Message.Contains("Playlist"))
                    {
                        ValidationMessage = ex.Message;
                        HasValidationError = true;
                    }
                },
                _playlistToEdit != null
                    ? $"Updating playlist '{_playlistToEdit.Title}' to '{PlaylistName}'"
                    : $"Creating new playlist '{PlaylistName}'",
                ErrorSeverity.NonCritical
            );
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