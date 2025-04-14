using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Profile.ViewModels
{
    public class ProfileUpdateMessage
    {
        public Profiles UpdatedProfile { get; }

        public ProfileUpdateMessage(Profiles profile)
        {
            UpdatedProfile = profile;
        }
    }

    public partial class ProfileDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly ProfileService _profileService;
        private readonly ProfileManager _profileManager;
        private readonly LocalizationService _localizationService;
        private readonly StandardImageService _standardImageService;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private ObservableCollection<Profiles> _profiles;

        [ObservableProperty]
        private bool _isCreating;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _newProfileName;

        [ObservableProperty]
        private Bitmap? _selectedImage;

        [ObservableProperty]
        private bool _hasSelectedImage;

        [ObservableProperty]
        private Profiles? _profileToEdit;

        private Stream? _selectedImageStream;
        private Dictionary<int, Bitmap> _profilePhotos = new();

        public ProfileDialogViewModel(
            Window dialog,
            ProfileService profileService,
            ProfileManager profileManager,
            LocalizationService localizationService,
            StandardImageService standardImageService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _dialog = dialog;
            _profileService = profileService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _standardImageService = standardImageService;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
            Profiles = new ObservableCollection<Profiles>();

            LoadProfiles();
        }

        private async void LoadProfiles()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Dispose existing profile photos
                    foreach (var photo in _profilePhotos.Values)
                    {
                        photo?.Dispose();
                    }
                    _profilePhotos.Clear();

                    var dbProfiles = await _profileService.GetAllProfiles();
                    Profiles.Clear();

                    foreach (var profile in dbProfiles)
                    {
                        if (profile.PhotoID > 0)
                        {
                            await _errorHandlingService.SafeExecuteAsync(
                                async () => 
                                profile.Photo = await _profileService.LoadProfilePhotoAsync(profile.PhotoID, "medium", true),
                                $"Loading photo for profile {profile.ProfileName}",
                                ErrorSeverity.NonCritical,
                                false
                            );
                        }
                        Profiles.Add(profile);
                    }
                },
                "Loading profiles",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private void ShowCreateForm()
        {
            IsCreating = true;
            NewProfileName = string.Empty;
            ClearImageSelection();

        }

        [RelayCommand]
        private void CancelCreate()
        {
            IsCreating = false;
            IsEditing = false;
            ProfileToEdit = null;
            NewProfileName = string.Empty;
            ClearImageSelection();
        }

        [RelayCommand]
        private async Task CreateProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName)) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = new Profiles
                    {
                        ProfileName = NewProfileName,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    if (_selectedImageStream != null)
                    {
                        _selectedImageStream.Position = 0;
                        profile.PhotoID = await _profileService.AddProfile(profile, _selectedImageStream);
                    }
                    else
                    {
                        profile.PhotoID = await _profileService.AddProfile(profile);
                    }

                    LoadProfiles();
                    IsCreating = false;
                    NewProfileName = string.Empty;
                    ClearImageSelection();
                },
                "Creating new profile",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task SelectProfile(Profiles profile)
        {
            if (profile == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid profile selection",
                    "Attempted to select a null profile",
                    null,
                    false
                );
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _profileManager.SwitchProfile(profile);
                    _messenger.Send(new ProfileUpdateMessage(profile));
                    _dialog.Close(profile);
                },
                $"Switching to profile {profile.ProfileName}",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async void EditProfile(Profiles profile)
        {
            if (profile == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid profile edit",
                    "Attempted to edit a null profile",
                    null,
                    false
                );
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    IsEditing = true;
                    IsCreating = true;
                    ProfileToEdit = profile;
                    NewProfileName = profile.ProfileName;

                    if (profile.Photo != null || profile.PhotoID > 0)
                    {
                        await _errorHandlingService.SafeExecuteAsync(
                            async () =>
                            {
                                SelectedImage = await _profileService.LoadProfilePhotoAsync(profile.PhotoID, "medium", true);
                                HasSelectedImage = true;
                            },
                            $"Loading profile photo for editing {profile.ProfileName}",
                            ErrorSeverity.NonCritical,
                            false
                        );
                    }
                    else
                    {
                        ClearImageSelection();
                    }
                },
                $"Editing profile {profile.ProfileName}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        [RelayCommand]
        private async Task SaveEditedProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName) || ProfileToEdit == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid profile data",
                    "Cannot save profile with empty name or null profile",
                    null,
                    false
                );
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    ProfileToEdit.ProfileName = NewProfileName;
                    ProfileToEdit.UpdatedAt = DateTime.Now;

                    if (_selectedImageStream != null)
                    {
                        _selectedImageStream.Position = 0;
                        await _profileService.UpdateProfile(ProfileToEdit, _selectedImageStream);
                    }
                    else
                    {
                        await _profileService.UpdateProfile(ProfileToEdit);
                    }

                    LoadProfiles();
                    IsEditing = false;
                    IsCreating = false;
                    ProfileToEdit = null;
                    NewProfileName = string.Empty;
                    ClearImageSelection();
                },
                $"Saving changes to profile {ProfileToEdit.ProfileName}",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task DeleteProfile(Profiles profile)
        {
            if (profile == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid profile deletion",
                    "Attempted to delete a null profile",
                    null,
                    false
                );
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _profileService.DeleteProfile(profile.ProfileID);
                    LoadProfiles();
                },
                $"Deleting profile {profile.ProfileName}",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }

        [RelayCommand]
        private async Task SelectProfilePhoto()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var options = new FilePickerOpenOptions
                    {
                        AllowMultiple = false,
                        Title = _localizationService["SelectProfilePhoto"],
                        FileTypeFilter = new FilePickerFileType[]
                        {
                            new("Image Files")
                            {
                                Patterns = new[] { "*.jpg", "*.jpeg", "*.png" },
                                MimeTypes = new[] { "image/jpeg", "image/png" }
                            }
                        }
                    };

                    var result = await _dialog.StorageProvider.OpenFilePickerAsync(options);
                    if (result.Count > 0)
                    {
                        _selectedImageStream?.Dispose();
                        _selectedImageStream = await result[0].OpenReadAsync();
                        var stream = await result[0].OpenReadAsync();
                        SelectedImage = new Bitmap(stream);
                        HasSelectedImage = true;
                    }
                },
                "Selecting profile photo",
                ErrorSeverity.NonCritical
            );
        }

        private void ClearImageSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    _selectedImageStream?.Dispose();
                    _selectedImageStream = null;
                    SelectedImage?.Dispose();
                    SelectedImage = null;
                    HasSelectedImage = false;
                },
                "Clearing image selection",
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Updates visibility tracking for a profile's photo
        /// </summary>
        public async Task NotifyProfilePhotoVisible(Profiles profile, bool isVisible)
        {
            if (profile == null || profile.PhotoID <= 0) return;

            // Get the media path for this photo
            var media = await _profileService.GetMediaByProfileId(profile.PhotoID);
            if (media != null && !string.IsNullOrEmpty(media.CoverPath))
            {
                // Update the visibility status
                await _standardImageService.NotifyImageVisible(media.CoverPath, isVisible);
            }
        }
    }
}