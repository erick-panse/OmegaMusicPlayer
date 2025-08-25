using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.PresetTheme;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class SetupViewModel : ObservableObject, IDisposable
    {
        private readonly Window _window;
        private readonly LocalizationService _localizationService;
        private readonly ThemeService _themeService;
        private readonly ProfileService _profileService;
        private readonly ProfileManager _profileManager;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly DirectoriesService _directoriesService;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        public enum SetupStep
        {
            Welcome,
            Language,
            Theme,
            ProfileName,
            LibraryFolder,
            Completed
        }

        [ObservableProperty]
        private SetupStep _currentStep = SetupStep.Welcome;

        [ObservableProperty]
        private ObservableCollection<LanguageInfo> _availableLanguages = new();

        [ObservableProperty]
        private LanguageInfo _selectedLanguage;

        [ObservableProperty]
        private ObservableCollection<string> _availableThemes = new();

        [ObservableProperty]
        private string _selectedTheme;

        [ObservableProperty]
        private string _profileName;

        [ObservableProperty]
        private ObservableCollection<string> _selectedFolders = new();

        [ObservableProperty]
        private bool _isWelcomeComplete = false;

        [ObservableProperty]
        private bool _canGoNext = false;

        [ObservableProperty]
        private bool _canGoBack = false;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _currentStepTitle = "";

        [ObservableProperty]
        private string _currentStepDescription = "";

        [ObservableProperty]
        private Bitmap? _selectedImage;

        [ObservableProperty]
        private string _profileNameValidationMessage = "";

        [ObservableProperty]
        private bool _hasValidationError = false;

        [ObservableProperty]
        private bool _isValidating = false;

        private DispatcherTimer _welcomeTimer;
        private bool _isUpdating = false;

        private Stream? _selectedImageStream;
        private Dictionary<int, Bitmap> _profilePhotos = new();

        private PresetTheme _currentThemeType = PresetTheme.Neon;

        public SetupViewModel(Window window)
        {
            _window = window;

            // Get services from DI container
            _localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            _themeService = App.ServiceProvider.GetRequiredService<ThemeService>();
            _profileService = App.ServiceProvider.GetRequiredService<ProfileService>();
            _profileManager = App.ServiceProvider.GetRequiredService<ProfileManager>();
            _globalConfigService = App.ServiceProvider.GetRequiredService<GlobalConfigurationService>();
            _directoriesService = App.ServiceProvider.GetRequiredService<DirectoriesService>();
            _messenger = App.ServiceProvider.GetRequiredService<IMessenger>();
            _errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

            // Subscribe to language changes to update UI
            _messenger.Register<LanguageChangedMessage>(this, (r, m) =>
            {
                // Use dispatcher to ensure UI updates happen on UI thread
                Dispatcher.UIThread.Post(() => UpdateDisplayTexts());
            });

            InitializeSetup();
        }

        private void InitializeSetup()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                // Initialize languages dynamically
                InitializeLanguages();

                // Initialize themes
                InitializeThemes();

                // Set up welcome timer
                _welcomeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(4) // 4 seconds welcome
                };
                _welcomeTimer.Tick += (s, e) =>
                {
                    _welcomeTimer.Stop();
                    IsWelcomeComplete = true;
                    NextStep();
                };

                // Start with welcome step
                UpdateStepContent();

                // Start welcome timer
                _welcomeTimer.Start();
            }, 
            "Initializing setup wizard",
            ErrorSeverity.NonCritical,
            false);
        }

        private void InitializeLanguages()
        {
            AvailableLanguages.Clear();

            // Get all available languages from the localization service
            var languages = _localizationService.AvailableLanguages;
            foreach (var language in languages)
            {
                AvailableLanguages.Add(language);
            }

            // Default to the system default or first available language
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.IsDefault)
                              ?? AvailableLanguages.FirstOrDefault();
        }

        private void InitializeThemes()
        {
            AvailableThemes.Clear();
            AvailableThemes.Add(_localizationService["ThemeNeon"]);
            AvailableThemes.Add(_localizationService["ThemeDark"]);
            AvailableThemes.Add(_localizationService["ThemeCrimson"]);
            AvailableThemes.Add(_localizationService["ThemeTropical"]);
            AvailableThemes.Add(_localizationService["ThemeLight"]);

            // Default to Dark theme
            SelectedTheme = AvailableThemes.First();
        }

        private void UpdateDisplayTexts()
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                // Store current theme type before updating
                if (!string.IsNullOrEmpty(SelectedTheme))
                {
                    _currentThemeType = GetThemeTypeFromName(SelectedTheme);
                }

                // IMPORTANT: Update theme text instead of recreating collection
                for (int i = 0; i < AvailableThemes.Count; i++)
                {
                    string themeText = i switch
                    {
                        0 => _localizationService["ThemeNeon"],
                        1 => _localizationService["ThemeDark"],
                        2 => _localizationService["ThemeCrimson"],
                        3 => _localizationService["ThemeTropical"],
                        4 => _localizationService["ThemeLight"],
                        _ => AvailableThemes[i]
                    };

                    // Only update if the text has changed
                    if (AvailableThemes[i] != themeText)
                    {
                        AvailableThemes[i] = themeText;
                    }
                }

                // Restore theme selection based on theme type 
                string newThemeName = _currentThemeType switch
                {
                    PresetTheme.Neon => _localizationService["ThemeNeon"],
                    PresetTheme.Dark => _localizationService["ThemeDark"],
                    PresetTheme.Crimson => _localizationService["ThemeCrimson"],
                    PresetTheme.Tropical => _localizationService["ThemeTropical"],
                    PresetTheme.Light => _localizationService["ThemeLight"],
                    _ => _localizationService["ThemeNeon"]
                };

                if (SelectedTheme != newThemeName)
                {
                    SelectedTheme = newThemeName;
                }

                // Force update of step content with new language
                UpdateStepContent();

                // Explicitly notify that these properties have changed
                OnPropertyChanged(nameof(CurrentStepTitle));
                OnPropertyChanged(nameof(CurrentStepDescription));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateStepContent()
        {
            switch (CurrentStep)
            {
                case SetupStep.Welcome:
                    CurrentStepTitle = _localizationService["WelcomeToOmegaPlayer"];
                    CurrentStepDescription = _localizationService["FirstTimeSetupDescription"];
                    CanGoNext = IsWelcomeComplete;
                    CanGoBack = false;
                    break;

                case SetupStep.Language:
                    CurrentStepTitle = _localizationService["SelectLanguage"];
                    CurrentStepDescription = _localizationService["ChooseYourPreferredLanguage"];
                    CanGoNext = SelectedLanguage != null;
                    CanGoBack = true;
                    break;

                case SetupStep.Theme:
                    CurrentStepTitle = _localizationService["SelectTheme"];
                    CurrentStepDescription = _localizationService["ChooseYourPreferredTheme"];
                    CanGoNext = !string.IsNullOrEmpty(SelectedTheme);
                    CanGoBack = true;
                    break;

                case SetupStep.ProfileName:
                    CurrentStepTitle = _localizationService["CreateProfile"];
                    CurrentStepDescription = _localizationService["EnterYourProfileName"];
                    CanGoNext = !string.IsNullOrWhiteSpace(ProfileName) && !HasValidationError;
                    CanGoBack = true;
                    break;

                case SetupStep.LibraryFolder:
                    CurrentStepTitle = _localizationService["SelectMusicLibrary"];
                    CurrentStepDescription = _localizationService["SelectFoldersContainingMusic"];
                    CanGoNext = SelectedFolders.Count > 0;
                    CanGoBack = true;
                    break;
            }
        }

        [RelayCommand]
        private void NextStep()
        {
            if (!CanGoNext) return;

            _errorHandlingService.SafeExecute(() =>
            {
                switch (CurrentStep)
                {
                    case SetupStep.Welcome:
                        CurrentStep = SetupStep.Language;
                        break;
                    case SetupStep.Language:
                        // Language is now applied immediately in property change handler
                        CurrentStep = SetupStep.Theme;
                        break;
                    case SetupStep.Theme:
                        // Theme is now applied immediately in property change handler
                        CurrentStep = SetupStep.ProfileName;
                        break;
                    case SetupStep.ProfileName:
                        CurrentStep = SetupStep.LibraryFolder;
                        break;
                    case SetupStep.LibraryFolder:
                        CompleteSetup();
                        return;
                }

                UpdateStepContent();
            }, 
            "Moving to next setup step",
            ErrorSeverity.NonCritical,
            false);
        }

        [RelayCommand]
        private void PreviousStep()
        {
            if (!CanGoBack) return;

            _errorHandlingService.SafeExecute(() =>
            {
                switch (CurrentStep)
                {
                    case SetupStep.Language:
                        CurrentStep = SetupStep.Welcome;
                        break;
                    case SetupStep.Theme:
                        CurrentStep = SetupStep.Language;
                        break;
                    case SetupStep.ProfileName:
                        CurrentStep = SetupStep.Theme;
                        break;
                    case SetupStep.LibraryFolder:
                        CurrentStep = SetupStep.ProfileName;
                        break;
                }

                UpdateStepContent();
            }, 
            "Moving to previous setup step",
            ErrorSeverity.NonCritical,
            false);
        }

        [RelayCommand]
        private async Task AddMusicFolder()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                var folderPicker = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = _localizationService["SelectMusicDirectory"],
                    AllowMultiple = true
                });

                foreach (var folder in folderPicker)
                {
                    var path = folder.Path.LocalPath;
                    if (!SelectedFolders.Contains(path))
                    {
                        SelectedFolders.Add(path);
                    }
                }

                UpdateStepContent();
            }, 
            "Adding music folder",
            ErrorSeverity.NonCritical,
            false); ;
        }

        [RelayCommand]
        private void RemoveMusicFolder(string folderPath)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                SelectedFolders.Remove(folderPath);
                UpdateStepContent();
            }, 
            "Removing music folder",
            ErrorSeverity.NonCritical,
            false);
        }

        private PresetTheme GetThemeTypeFromName(string themeName)
        {
            return _errorHandlingService.SafeExecute(() =>
            {
                // Check against localized names
                if (themeName == _localizationService["ThemeNeon"]) return PresetTheme.Neon;
                if (themeName == _localizationService["ThemeDark"]) return PresetTheme.Dark;
                if (themeName == _localizationService["ThemeCrimson"]) return PresetTheme.Crimson;
                if (themeName == _localizationService["ThemeTropical"]) return PresetTheme.Tropical;
                if (themeName == _localizationService["ThemeLight"]) return PresetTheme.Light;

                // Fallback to checking English names (for backward compatibility)
                if (themeName == "Neon") return PresetTheme.Neon;
                if (themeName == "Dark") return PresetTheme.Dark;
                if (themeName == "Crimson") return PresetTheme.Crimson;
                if (themeName == "Tropical") return PresetTheme.Tropical;
                if (themeName == "Light") return PresetTheme.Light;

                // Default
                return PresetTheme.Neon;
            }, 
            "Determining theme type from name", 
            PresetTheme.Neon,
            ErrorSeverity.NonCritical,
            false);
        }

        private async void CompleteSetup()
        {
            IsLoading = true;

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // 1. Final validation check before completing setup
                await ValidateProfileName();
                if (HasValidationError)
                {
                    // Send back to step ProfileName if name is invalid
                    CurrentStep = SetupStep.ProfileName;
                    IsLoading = false;
                    UpdateStepContent();
                    return;
                }

                try
                {
                    // 2. Create/Update the profile
                    await CreateProfile();

                    // 3. Save theme preference to profile config
                    await SaveThemePreference();

                    // 4. Add selected directories
                    await AddSelectedDirectories();

                    // 5. Close the setup window
                    CurrentStep = SetupStep.Completed;
                    _window.Close(true);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("name"))
                {
                    // Handle validation errors that occur during profile creation
                    ProfileNameValidationMessage = ex.Message;
                    HasValidationError = true;
                    UpdateStepContent(); // Update UI to reflect validation error

                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        _localizationService["ErrorCreatingProfile"],
                        ex.Message,
                        null,
                        true);
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        _localizationService["SetupError"],
                        ex.Message,
                        ex,
                        true);
                }

            }, "Completing setup", ErrorSeverity.NonCritical);

            IsLoading = false;
        }

        private async Task CreateProfile()
        {
            // Validate before creating
            await ValidateProfileName();
            if (HasValidationError)
            {
                // Send back to step ProfileName if name is invalid
                CurrentStep = SetupStep.ProfileName;
                IsLoading = false;
                UpdateStepContent();
                return;
            }

            // Get the current profile (should be the default one created during app startup)
            var currentProfile = await _profileManager.GetCurrentProfileAsync();

            if (currentProfile != null && !string.IsNullOrWhiteSpace(ProfileName))
            {
                // Update the default profile name
                currentProfile.ProfileName = ProfileName.Trim();

                try
                {
                    if (_selectedImageStream != null)
                    {
                        _selectedImageStream.Position = 0;
                        await _profileService.UpdateProfile(currentProfile, _selectedImageStream);
                    }
                    else
                    {
                        await _profileService.UpdateProfile(currentProfile);
                    }
                }
                catch (ArgumentException ex) when (ex.Message.Contains("name"))
                {
                    ProfileNameValidationMessage = ex.Message;
                    HasValidationError = true;
                    throw; // Re-throw to be handled by calling method
                }
            }
        }

        private async Task ValidateProfileName()
        {
            if (IsValidating) return;

            IsValidating = true;

            try
            {
                var validationMessage = await _profileService.ValidateProfileNameAsync(ProfileName);

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

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(ProfileName))
            {
                // Debounce validation to avoid excessive calls
                _ = Task.Delay(300).ContinueWith(async _ => await ValidateProfileName());
            }
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

                    var result = await _window.StorageProvider.OpenFilePickerAsync(options);
                    if (result.Count > 0)
                    {
                        _selectedImageStream?.Dispose();
                        _selectedImageStream = await result[0].OpenReadAsync();
                        var stream = await result[0].OpenReadAsync();
                        SelectedImage = new Bitmap(stream);
                    }
                },
                _localizationService["ErrorSelectingProfilePhoto"],
                ErrorSeverity.NonCritical,
                false);
        }
        private async Task SaveThemePreference()
        {
            var currentProfile = await _profileManager.GetCurrentProfileAsync();
            if (currentProfile == null) return;

            var themeType = GetThemeTypeFromName(SelectedTheme);
            var themeConfig = new ThemeConfiguration
            {
                ThemeType = themeType
            };

            var profileConfigService = App.ServiceProvider.GetRequiredService<ProfileConfigurationService>();
            await profileConfigService.UpdateProfileTheme(currentProfile.ProfileID, themeConfig);
        }

        private async Task AddSelectedDirectories()
        {
            foreach (var folderPath in SelectedFolders)
            {
                var directory = new Directories { DirPath = folderPath };
                await _directoriesService.AddDirectory(directory);
            }

            // Notify that directories have changed
            _messenger.Send(new DirectoriesChangedMessage());
        }

        [RelayCommand]
        private void Cancel()
        {
            _window.Close(false);
        }

        partial void OnSelectedLanguageChanged(LanguageInfo value)
        {
            if (value == null || _isUpdating) return;

            _errorHandlingService.SafeExecute(() =>
            {
                // Change the application language immediately
                _localizationService.ChangeLanguage(value.LanguageCode);

                // Update the global configuration
                _ = _globalConfigService.UpdateLanguage(value.LanguageCode);

                // Notify UI of language change
                _messenger.Send(new LanguageChangedMessage(value.LanguageCode));
            }, 
            "Applying language selection",
            ErrorSeverity.NonCritical,
            false);

            // Update step content immediately after language change
            if (!_isUpdating)
            {
                UpdateStepContent();
            }
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || _isUpdating) return;

            _errorHandlingService.SafeExecute(() =>
            {
                // Store the theme type when selection changes
                _currentThemeType = GetThemeTypeFromName(value);

                // Apply the theme immediately
                _themeService.ApplyPresetTheme(_currentThemeType);
            }, 
            "Applying theme selection",
            ErrorSeverity.NonCritical,
            false);

            UpdateStepContent();
        }

        partial void OnProfileNameChanged(string value)
        {
            // Update step content to refresh CanGoNext state
            UpdateStepContent();
        }

        partial void OnHasValidationErrorChanged(bool value)
        {
            // Update step content when validation state changes
            UpdateStepContent();
        }

        partial void OnIsWelcomeCompleteChanged(bool value)
        {
            UpdateStepContent();
        }

        public void Dispose()
        {
            _welcomeTimer?.Stop();
            _selectedImageStream?.Dispose();
            _messenger?.UnregisterAll(this);

            // Dispose profile photos if any
            foreach (var photo in _profilePhotos.Values)
            {
                photo?.Dispose();
            }
            _profilePhotos.Clear();
        }
    }
}