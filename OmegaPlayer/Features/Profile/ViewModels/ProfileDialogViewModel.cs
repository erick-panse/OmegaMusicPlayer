using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Infrastructure.Services;
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
        private readonly IMessenger _messenger;

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
            IMessenger messenger)
        {
            _dialog = dialog;
            _profileService = profileService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _messenger = messenger;
            Profiles = new ObservableCollection<Profiles>();

            LoadProfiles();
        }

        private async void LoadProfiles()
        {
            try
            {
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
                        try
                        {
                            profile.Photo = await _profileService.LoadMediumQualityProfilePhoto(profile.PhotoID);
                        }
                        catch
                        {
                            // Ignore photo loading errors
                        }
                    }
                    Profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profiles: {ex.Message}");
            }
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
            SelectedImage?.Dispose();
            SelectedImage = null;
            _selectedImageStream?.Dispose();
            _selectedImageStream = null;
            HasSelectedImage = false;
        }

        [RelayCommand]
        private async Task CreateProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName)) return;

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
        }

        [RelayCommand]
        private async Task SelectProfile(Profiles profile)
        {
            await _profileManager.SwitchProfile(profile);
            _messenger.Send(new ProfileUpdateMessage(profile));
            _dialog.Close(profile);
        }

        [RelayCommand]
        private async void EditProfile(Profiles profile)
        {
            IsEditing = true;
            IsCreating = true;
            ProfileToEdit = profile;
            NewProfileName = profile.ProfileName;

            if (profile.Photo != null)
            {
                try
                {
                    SelectedImage = await _profileService.LoadMediumQualityProfilePhoto(profile.PhotoID);
                    HasSelectedImage = true;
                }
                catch
                {
                    ClearImageSelection();
                }
            }
            else
            {
                ClearImageSelection();
            }
        }

        [RelayCommand]
        private async Task SaveEditedProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName) || ProfileToEdit == null) return;

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
        }

        [RelayCommand]
        private async Task DeleteProfile(Profiles profile)
        {
            await _profileService.DeleteProfile(profile.ProfileID);
            LoadProfiles();
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }

        [RelayCommand]
        private async Task SelectProfilePhoto()
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
                try
                {
                    _selectedImageStream?.Dispose();
                    _selectedImageStream = await result[0].OpenReadAsync();
                    var stream = await result[0].OpenReadAsync();
                    SelectedImage = new Bitmap(stream);
                    HasSelectedImage = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading image: {ex.Message}");
                    HasSelectedImage = false;
                }
            }
        }

        private void ClearImageSelection()
        {
            _selectedImageStream?.Dispose();
            _selectedImageStream = null;
            SelectedImage?.Dispose();
            SelectedImage = null;
            HasSelectedImage = false;
        }
    }
}