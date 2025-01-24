using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using NAudio.Wave;
using OmegaPlayer.Features.Profile.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Profile.ViewModels
{
    public partial class ProfileDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;

        [ObservableProperty]
        private ObservableCollection<UserProfile> _profiles;

        [ObservableProperty]
        private bool _isCreating;

        [ObservableProperty]
        private string _newProfileName;

        [ObservableProperty]
        private Bitmap _selectedImage;

        [ObservableProperty]
        private bool _hasSelectedImage;
        public ProfileDialogViewModel(Window dialog)
        {
            _dialog = dialog;
            // Temporary test data
            Profiles = new ObservableCollection<UserProfile>
            {
                new UserProfile { Name = "Default User", PIcon = Application.Current.FindResource("ProfileIcon") },
                new UserProfile { Name = "Test User", PIcon = Application.Current.FindResource("ProfileIcon")  }
            };
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
            NewProfileName = string.Empty;
        }

        [RelayCommand]
        private void CreateProfile()
        {
            if (!string.IsNullOrWhiteSpace(NewProfileName))
            {
                Profiles.Add(new UserProfile { Name = NewProfileName });
                IsCreating = false;
                NewProfileName = string.Empty;
                ClearImageSelection();
            }
        }

        [RelayCommand]
        private void SelectProfile(UserProfile profile)
        {
            // In real implementation, switch to selected profile
            _dialog.Close(profile);
        }

        [RelayCommand]
        private void EditProfile(UserProfile profile)
        {
            // Implement edit logic
        }

        [RelayCommand]
        private void DeleteProfile(UserProfile profile)
        {
            Profiles.Remove(profile);
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
                Title = "Select Profile Photo",
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
                    await using var stream = await result[0].OpenReadAsync();
                    SelectedImage = new Bitmap(stream);
                    HasSelectedImage = true;
                }
                catch (Exception ex)
                {
                    // Handle error loading image
                    Console.WriteLine($"Error loading image: {ex.Message}");
                    HasSelectedImage = false;
                }
            }
        }

        private void ClearImageSelection()
        {
            SelectedImage?.Dispose();
            SelectedImage = null;
            HasSelectedImage = false;
        }

    }

    public class UserProfile
    {
        public string Name { get; set; }
        public object PIcon { get; set; }
    }
}