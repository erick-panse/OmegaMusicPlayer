using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Profile.Models;
using OmegaMusicPlayer.Features.Profile.Services;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Profile.ViewModels
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

        [ObservableProperty]
        private string _profileNameValidationMessage;

        [ObservableProperty]
        private bool _hasValidationError;

        [ObservableProperty]
        private bool _isValidating;

        [ObservableProperty]
        private bool _isProfileLimitReached;

        [ObservableProperty]
        private string _profileLimitMessage;

        [ObservableProperty]
        private bool _canDeleteProfiles;

        [ObservableProperty]
        private Profiles _currentActiveProfile;

        private const int MAX_PROFILES = 20;

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
                    // Get current active profile
                    CurrentActiveProfile = await _profileManager.GetCurrentProfileAsync();

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
                        // Set whether this profile can be deleted
                        profile.CanBeDeleted = dbProfiles.Count > 1 &&
                                              (CurrentActiveProfile == null || profile.ProfileID != CurrentActiveProfile.ProfileID);

                        if (profile.PhotoID > 0)
                        {
                            // Load the image in the background with lower priority
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    profile.Photo = await _profileService.LoadProfilePhotoAsync(profile.PhotoID, "medium", true);
                                }
                                catch (Exception ex)
                                {
                                    _errorHandlingService.LogError(
                                        ErrorSeverity.NonCritical,
                                        "Error loading profile image",
                                        ex.Message,
                                        ex,
                                        false);
                                }
                            });
                        }
                        Profiles.Add(profile);
                    }

                    // Check if profile limit is reached
                    IsProfileLimitReached = Profiles.Count >= MAX_PROFILES;
                    ProfileLimitMessage = IsProfileLimitReached
                        ? _localizationService["ProfileLimitReachedFirstHalf"] + Profiles.Count + "/" + MAX_PROFILES + _localizationService["ProfileLimitReachedSecondHalf"]
                        : $"{Profiles.Count}/{MAX_PROFILES} " + _localizationService["Profiles"];

                    // Check if profiles can be deleted (more than one profile exists)
                    CanDeleteProfiles = Profiles.Count > 1;
                },
                _localizationService["ErrorLoadingProfiles"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        private async Task ShowCreateForm()
        {
            // Check profile limit before showing create form
            var profileCount = await _profileService.GetProfileCount();
            if (profileCount >= MAX_PROFILES)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Profile limit reached",
                    $"Cannot create more than {MAX_PROFILES} profiles. Please delete an existing profile first.",
                    null,
                    false);
                return;
            }

            IsCreating = true;
            NewProfileName = string.Empty;
            ClearImageSelection();
        }

        private async Task ValidateProfileName()
        {
            if (IsValidating) return;

            IsValidating = true;

            try
            {
                if (string.IsNullOrWhiteSpace(NewProfileName))
                {
                    ProfileNameValidationMessage = string.Empty;
                    HasValidationError = false;
                    return;
                }

                var excludeId = IsEditing ? ProfileToEdit?.ProfileID : null; // this allows saving unchanged names
                var validationMessage = await _profileService.ValidateProfileNameAsync(NewProfileName, excludeId);

                ProfileNameValidationMessage = validationMessage ?? string.Empty;
                HasValidationError = !string.IsNullOrEmpty(validationMessage);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error validating profile name",
                    ex.Message,
                    ex,
                    false);
            }
            finally
            {
                IsValidating = false;
            }
        }

        // Override OnPropertyChanged to validate name changes
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(NewProfileName))
            {
                // Debounce validation to avoid excessive calls
                _ = Task.Delay(300).ContinueWith(async _ => await ValidateProfileName());
            }
        }

        [RelayCommand]
        private void CancelCreate()
        {
            IsCreating = false;
            IsEditing = false;
            ProfileToEdit = null;
            NewProfileName = string.Empty;
            ProfileNameValidationMessage = string.Empty;
            HasValidationError = false;
            ClearImageSelection();
        }

        [RelayCommand]
        private async Task CreateProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
            {
                ProfileNameValidationMessage = _localizationService["ProfileNameEmpty"];
                HasValidationError = true;
                return;
            }

            // Validate before creating
            await ValidateProfileName();
            if (HasValidationError)
                return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = new Profiles
                    {
                        ProfileName = NewProfileName,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    try
                    {
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
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot create more than 20 profiles"))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Profile limit reached",
                            ex.Message,
                            null,
                            true);

                        // Refresh the profile list to update the limit status
                        LoadProfiles();
                    }
                    catch (ArgumentException ex) when (ex.Message.Contains("name"))
                    {
                        ProfileNameValidationMessage = ex.Message;
                        HasValidationError = true;
                    }
                },
                _localizationService["ErrorCreatingProfile"],
                ErrorSeverity.NonCritical,
                true);
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
                    false);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _profileManager.SwitchProfile(profile);
                    _messenger.Send(new ProfileUpdateMessage(profile));
                    _dialog.Close(profile);
                },
               _localizationService["ErrorSwitchingProfile"] + profile.ProfileName,
                ErrorSeverity.NonCritical,
                true);
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
                    false);
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
                _localizationService["ErrorEditingProfile"] + profile.ProfileName,
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        private async Task SaveEditedProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName))
            {
                ProfileNameValidationMessage = _localizationService["ProfileNameEmpty"];
                HasValidationError = true;
                return;
            }

            if (ProfileToEdit == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid profile data",
                    "Cannot save profile with null profile",
                    null,
                    false);
                return;
            }

            // Validate before saving
            await ValidateProfileName();
            if (HasValidationError)
                return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    ProfileToEdit.ProfileName = NewProfileName.Trim();
                    ProfileToEdit.UpdatedAt = DateTime.UtcNow;

                    try
                    {
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
                        ProfileNameValidationMessage = string.Empty;
                        HasValidationError = false;
                        ClearImageSelection();
                    }
                    catch (ArgumentException ex) when (ex.Message.Contains("name"))
                    {
                        ProfileNameValidationMessage = ex.Message;
                        HasValidationError = true;
                    }
                },
                _localizationService["ErrorSavingProfile"] + ProfileToEdit.ProfileName,
                ErrorSeverity.NonCritical,
                true);
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

            // Check if this is the current profile
            if (CurrentActiveProfile != null && profile.ProfileID == CurrentActiveProfile.ProfileID)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Cannot delete current profile",
                    "You cannot delete the profile that is currently active. Switch to another profile first.",
                    null,
                    true);
                return;
            }

            // Double-check that we can delete profiles
            if (!CanDeleteProfiles)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Cannot delete last profile",
                    "At least one profile must remain in the system.",
                    null,
                    true);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    try
                    {
                        await _profileService.DeleteProfile(profile.ProfileID);
                        LoadProfiles();
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot delete the last profile"))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Cannot delete last profile",
                            ex.Message,
                            null,
                            true);
                    }
                },
                _localizationService["ErrorDeletingProfile"] + profile.ProfileName,
                ErrorSeverity.NonCritical,
                true);
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
                _localizationService["ErrorSelectingProfilePhoto"],
                ErrorSeverity.NonCritical,
                true);
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
                false);
        }
    }
}