using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Home.ViewModels;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Threading.Tasks;
using OmegaPlayer.Core.Navigation.Services;
using CommunityToolkit.Mvvm.Messaging; 
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using OmegaPlayer.Features.Profile.Views;
using Avalonia;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Features.Profile.ViewModels;
using Avalonia.Media.Imaging;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Configuration.Views;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly DirectoriesService _directoryService;
        private readonly TrackSortService _trackSortService;
        private readonly ProfileManager _profileManager;
        private readonly AudioMonitorService _audioMonitorService;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private bool _showSortingControls;

        [ObservableProperty]
        private bool _showBackButton;

        [ObservableProperty]
        private SortType _selectedSortType = SortType.Name;

        [ObservableProperty]
        private SortDirection _sortDirection = SortDirection.Ascending;

        [ObservableProperty]
        private string _selectedSortDirectionText = "A-Z"; // Default value

        [ObservableProperty]
        private string _selectedSortTypeText = "Name"; // Default value

        [ObservableProperty]
        private Bitmap _currentProfilePhoto;

        public ObservableCollection<string> AvailableSortTypes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableDirectionTypes { get; } = new() { "A-Z", "Z-A" };

        [ObservableProperty]
        private bool _showViewTypeButtons = false;

        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                SetProperty(ref _currentPage, value);

                // Update sorting controls visibility based on current view
                if (value is LibraryViewModel libraryVM)
                {
                    // Show sorting controls except for NowPlaying and Playlist content types
                    ShowSortingControls = libraryVM.ContentType != ContentType.NowPlaying &&
                                        libraryVM.ContentType != ContentType.Playlist;
                }
                else
                {
                    // Hide sorting controls for non-library views (Config, Home, etc.)
                    ShowSortingControls = false;
                }
            }
        }


        public TrackControlViewModel TrackControlViewModel { get; }

        public MainViewModel(
            DirectoryScannerService directoryScannerService,
            DirectoriesService directoryService,
            TrackControlViewModel trackControlViewModel,
            TrackSortService trackSortService,
            ProfileManager profileManager,
            AudioMonitorService audioMonitorService,
            ProfileConfigurationService profileConfigService,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            IMessenger messenger)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            TrackControlViewModel = trackControlViewModel;
            _trackSortService = trackSortService;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;
            _profileManager = profileManager;
            _audioMonitorService = audioMonitorService;
            _profileConfigService = profileConfigService;
            _messenger = messenger;

            // Set initial page
            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();

            UpdateAvailableSortTypes(ContentType.Library);
            InitializeProfilePhoto();
            StartBackgroundScan();
            InitializeAudioMonitoring();

            navigationService.NavigationRequested += async (s, e) => await NavigateToDetails(e.Type, e.Data);

            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileUpdate(m));
        }
        private async void InitializeAudioMonitoring()
        {
            try
            {
                // Wait for ProfileManager to initialize
                await _profileManager.InitializeAsync();

                // Get profile config
                var config = await _profileConfigService.GetProfileConfig(_profileManager.CurrentProfile.ProfileID);

                // Enable/disable dynamic pause based on profile settings
                _audioMonitorService.EnableDynamicPause(config.DynamicPause);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing audio monitoring: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Navigate(string destination)
        {
            //clear selected items in their respective views
            Type pageType = CurrentPage?.GetType();
            if (pageType != null)
            {
                var clearMethod = pageType.GetMethod("ClearSelection") ?? pageType.GetMethod("DeselectAllTracks");
                clearMethod?.Invoke(CurrentPage, null);
            }

            // Clear current view state in navigation service
            _navigationService.ClearCurrentView();
            ViewModelBase viewModel;
            ContentType contentType;
            switch (destination)
            {
                case "Home":
                    viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                    contentType = ContentType.Home;
                    break;
                case "Library":
                    viewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
                    contentType = ContentType.Library;
                    if (CurrentPage is LibraryViewModel libraryVM)
                        await libraryVM.NavigateBack();
                    else
                        await ((LibraryViewModel)viewModel).Initialize(false);
                    break;
                case "Artists":
                    viewModel = _serviceProvider.GetRequiredService<ArtistViewModel>();
                    contentType = ContentType.Artist;
                    break;
                case "Albums":
                    viewModel = _serviceProvider.GetRequiredService<AlbumViewModel>();
                    contentType = ContentType.Album;
                    break;
                case "Playlists":
                    viewModel = _serviceProvider.GetRequiredService<PlaylistViewModel>();
                    contentType = ContentType.Playlist;
                    break;
                case "Genres":
                    viewModel = _serviceProvider.GetRequiredService<GenreViewModel>();
                    contentType = ContentType.Genre;
                    break;
                case "Folders":
                    viewModel = _serviceProvider.GetRequiredService<FolderViewModel>();
                    contentType = ContentType.Folder;
                    break;
                case "Config":
                    var configView = _serviceProvider.GetRequiredService<ConfigView>();
                    viewModel = (ViewModelBase)configView.DataContext;
                    contentType = ContentType.Config;
                    break;
                default:
                    throw new ArgumentException($"Unknown destination: {destination}");
            }
            CurrentPage = viewModel;
            UpdateDirection(SelectedSortDirectionText);
            UpdateAvailableSortTypes(contentType);
            ShowViewTypeButtons = CurrentPage is LibraryViewModel;

            if (CurrentPage is LibraryViewModel _libraryVM)
            {
                ShowSortingControls = contentType != ContentType.NowPlaying && 
                    contentType != ContentType.Playlist;
            }
            else
            {
                ShowSortingControls = contentType != ContentType.Home && 
                    contentType != ContentType.Config;
            }
        }

        public async Task NavigateBackToLibrary()
        {
            await Navigate("Library");
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsExpanded = !IsExpanded;
        }

        public async Task NavigateToDetails(ContentType type, object data)
        {
            var detailsViewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
            await detailsViewModel.Initialize(true, type, data); // true since it's the details page
            CurrentPage = detailsViewModel;
            ShowViewTypeButtons = CurrentPage is LibraryViewModel;
            ShowSortingControls = false;

            // use hardcoded library content type to have the same sort types as library or else will have default sort type
            UpdateDirection(SelectedSortDirectionText);
            UpdateAvailableSortTypes(ContentType.Library);
        }

        private async Task HandleProfileUpdate(ProfileUpdateMessage message)
        {
            if (message.UpdatedProfile != null)
            {
                await _profileManager.SwitchProfile(message.UpdatedProfile);

                if (message.UpdatedProfile.PhotoID > 0)
                {
                    var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                    CurrentProfilePhoto = await profileService.LoadProfilePhoto(message.UpdatedProfile.PhotoID);
                }
                else
                {
                    CurrentProfilePhoto = null;
                }
            }
        }

        private async void InitializeProfilePhoto()
        {
            try
            {
                // Wait for ProfileManager to initialize
                await _profileManager.InitializeAsync();

                if (_profileManager.CurrentProfile?.PhotoID > 0)
                {
                    var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                    CurrentProfilePhoto = await profileService.LoadProfilePhoto(_profileManager.CurrentProfile.PhotoID);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profile photo: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SetViewType(string viewType)
        {
            ViewType parsedViewType = Enum.Parse<ViewType>(viewType, true);
            CurrentViewType = parsedViewType;

            // Update the current page's view type
            if (CurrentPage is LibraryViewModel libraryVM)
            {
                libraryVM.CurrentViewType = parsedViewType;
            }
        }

        private void UpdateAvailableSortTypes(ContentType contentType)
        {
            AvailableSortTypes.Clear();

            var types = contentType switch
            {
                ContentType.Library => ["Name", "Artist", "Album", "Duration", "Genre", "Release Date"],
                ContentType.NowPlaying or ContentType.Home => Array.Empty<string>(),
                _ => ["Name", "Duration"]
            };

            foreach (var type in types)
            {
                AvailableSortTypes.Add(type);
            }

            // Ensure selected type is valid for current context
            if (!AvailableSortTypes.Contains(SelectedSortTypeText))
            {
                SelectedSortTypeText = AvailableSortTypes.FirstOrDefault() ?? "Name";
            }
        }

        private void UpdateDirection(string direction)
        {
            if (AvailableDirectionTypes.Contains(direction))
            {
                SetSortDirection(direction);
            }
        }


        partial void OnSelectedSortTypeChanged(SortType value)
        {
            UpdateSorting();
        }

        partial void OnSortDirectionChanged(SortDirection value)
        {
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            _messenger.Send(new SortUpdateMessage(SelectedSortType, SortDirection));
        }

        [RelayCommand]
        public void SetSortDirection(string direction)
        {
            SelectedSortDirectionText = direction;
            SortDirection = direction.ToUpper() switch
            {
                "A-Z" => SortDirection.Ascending,
                "Z-A" => SortDirection.Descending,
                _ => SortDirection.Ascending
            };
        }

        [RelayCommand]
        public void SetSortType(string sortType)
        {
            SelectedSortTypeText = sortType;
            SelectedSortType = sortType.ToLower() switch
            {
                "name" => SortType.Name,
                "artist" => SortType.Artist,
                "album" => SortType.Album,
                "duration" => SortType.Duration,
                "genre" => SortType.Genre,
                "releasedate" => SortType.ReleaseDate,
                _ => SortType.Name
            };
        }

        [RelayCommand]
        public async Task OpenProfileDialog()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var dialog = new ProfileDialogView
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var result = await dialog.ShowDialog<Profiles>(mainWindow);
                if (result != null)
                {
                    // update tracks for new profile
                    var tracksUpdate = App.ServiceProvider.GetService<AllTracksRepository>();
                    await tracksUpdate.LoadTracks();
                }
            }
        }
        public async void StartBackgroundScan()
        {
            var directories = await _directoryService.GetAllDirectories();
            await _profileManager.InitializeAsync();
            await Task.Run(() => _directoryScannerService.ScanDirectoriesAsync(directories, _profileManager.CurrentProfile.ProfileID));
        }
    }
}