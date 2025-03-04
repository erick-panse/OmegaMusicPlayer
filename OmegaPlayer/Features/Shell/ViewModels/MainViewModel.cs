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
using OmegaPlayer.Features.Configuration.Views;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.UI;
using System.Collections.Generic;
using OmegaPlayer.Features.Search.ViewModels;
using Avalonia.Media;
using OmegaPlayer.Core.Messages;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly DirectoriesService _directoryService;
        private readonly SearchViewModel _searchViewModel;
        private readonly ProfileManager _profileManager;
        private readonly AudioMonitorService _audioMonitorService;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly StateManagerService _stateManager;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private ContentType _currentContentType = ContentType.Home;

        [ObservableProperty]
        private bool _showSortingControls;

        [ObservableProperty]
        private bool _showBackButton;

        [ObservableProperty]
        private SortType _selectedSortType;

        [ObservableProperty]
        private SortDirection _sortDirection;

        [ObservableProperty]
        private string _selectedSortDirectionText;

        [ObservableProperty]
        private string _selectedSortTypeText;

        [ObservableProperty]
        private Bitmap _currentProfilePhoto;

        [ObservableProperty]
        private bool _showViewTypeButtons = false;

        [ObservableProperty]
        private bool _showSearchBox;

        [ObservableProperty]
        private double _searchBoxWidth = 0;

        [ObservableProperty]
        private double _searchBoxOpacity = 0;

        [ObservableProperty]
        private StreamGeometry _searchToggleIcon;

        [ObservableProperty]
        private bool _showFloatingButtons;

        [ObservableProperty]
        private Action _saveReorderAction;

        [ObservableProperty]
        private Action _cancelReorderAction;


        [ObservableProperty]
        private Transform _sortIconTransform = new RotateTransform(180); // Default to arrow up (ascending)

        // Temporary settings that don't trigger loading until applied
        [ObservableProperty]
        private SortType _tempSortType;

        [ObservableProperty]
        private SortDirection _tempSortDirection;

        // Properties for UI binding of checked states
        [ObservableProperty]
        private bool _isTempSortTypeName;

        [ObservableProperty]
        private bool _isTempSortTypeArtist;

        [ObservableProperty]
        private bool _isTempSortTypeAlbum;

        [ObservableProperty]
        private bool _isTempSortTypeDuration;

        [ObservableProperty]
        private bool _isTempSortTypeGenre;

        [ObservableProperty]
        private bool _isTempSortTypeReleaseDate;

        [ObservableProperty]
        private bool _isTempSortDirectionAscending;

        [ObservableProperty]
        private bool _isTempSortDirectionDescending;

        [ObservableProperty]
        private bool _showNameSortOption = true;

        [ObservableProperty]
        private bool _showArtistSortOption = true;

        [ObservableProperty]
        private bool _showAlbumSortOption = true;

        [ObservableProperty]
        private bool _showDurationSortOption = true;

        [ObservableProperty]
        private bool _showGenreSortOption = true;

        [ObservableProperty]
        private bool _showReleaseDateSortOption = true;

        public ObservableCollection<string> AvailableSortTypes { get; } = new ObservableCollection<string>();

        private static readonly Dictionary<string, (SortType Type, string Display)> SortTypeMap = new()
        {
            { "name", (SortType.Name, "Name") },
            { "artist", (SortType.Artist, "Artist") },
            { "album", (SortType.Album, "Album") },
            { "duration", (SortType.Duration, "Duration") },
            { "genre", (SortType.Genre, "Genre") },
            { "release date", (SortType.ReleaseDate, "Release Date") }
        };

        private Dictionary<ContentType, ViewSortingState> _sortingStates = new();

        private string _currentView = "library";
        public string? CurrentView => _currentView;
        public SearchViewModel SearchViewModel => _searchViewModel;

        private ViewModelBase _currentPage;

        public TrackControlViewModel TrackControlViewModel { get; }

        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    UpdateSortingControlsVisibility(value);
                }
            }
        }


        public MainViewModel(
            DirectoryScannerService directoryScannerService,
            DirectoriesService directoryService,
            TrackControlViewModel trackControlViewModel,
            SearchViewModel searchViewModel,
            ProfileManager profileManager,
            AudioMonitorService audioMonitorService,
            ProfileConfigurationService profileConfigService,
            StateManagerService stateManagerService,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            IMessenger messenger)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            TrackControlViewModel = trackControlViewModel;
            _searchViewModel = searchViewModel;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;
            _profileManager = profileManager;
            _audioMonitorService = audioMonitorService;
            _profileConfigService = profileConfigService;
            _stateManager = stateManagerService;
            _messenger = messenger;

            // Set initial page
            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();

            InitializeProfilePhoto();
            StartBackgroundScan();
            InitializeAudioMonitoring();
            UpdateSearchIcon();

            navigationService.NavigationRequested += async (s, e) => await NavigateToDetails(e.Type, e.Data);

            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileUpdate(m));

            // Subscribe to state changes
            PropertyChanged += async (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(CurrentViewType):
                    case nameof(SelectedSortType):
                    case nameof(SortDirection):
                        await _stateManager.SaveCurrentState();
                        break;
                }
            };

            // Load initial state
            Task loadState = _stateManager.LoadAndApplyState();

            _messenger.Register<ReorderModeMessage>(this, (r, m) =>
            {
                ShowFloatingButtons = m.IsInReorderMode;
                SaveReorderAction = m.SaveAction;
                CancelReorderAction = m.CancelAction;
            });

            _messenger.Register<NavigationRequestMessage>(this, (r, m) => NavigateToDetails(m.ContentType, m.Data));
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
        public async Task Navigate(string destination)
        {
            //clear selected items in their respective views
            Type? pageType = CurrentPage?.GetType();
            if (pageType != null)
            {
                var clearMethod = pageType.GetMethod("ClearSelection") ?? pageType.GetMethod("DeselectAllTracks");
                clearMethod?.Invoke(CurrentPage, null);
            }

            ResetReorder();

            // Update current view
            _currentView = destination.ToLower();

            // Clear current view state in navigation service
            _navigationService.ClearCurrentView();
            ViewModelBase viewModel;
            ContentType contentType;
            switch (destination)
            {
                case "Home":
                    viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                    contentType = ContentType.Home;
                    ((HomeViewModel)viewModel).Initialize();
                    break;
                case "Library":
                    viewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
                    contentType = ContentType.Library;
                    await ((LibraryViewModel)viewModel).Initialize(false);
                    break;
                case "Artists":
                    viewModel = _serviceProvider.GetRequiredService<ArtistsViewModel>();
                    contentType = ContentType.Artist;
                    break;
                case "Albums":
                    viewModel = _serviceProvider.GetRequiredService<AlbumsViewModel>();
                    contentType = ContentType.Album;
                    break;
                case "Playlists":
                    viewModel = _serviceProvider.GetRequiredService<PlaylistsViewModel>();
                    contentType = ContentType.Playlist;
                    ((PlaylistsViewModel)viewModel).LoadInitialPlaylists();
                    break;
                case "Genres":
                    viewModel = _serviceProvider.GetRequiredService<GenresViewModel>();
                    contentType = ContentType.Genre;
                    break;
                case "Folders":
                    viewModel = _serviceProvider.GetRequiredService<FoldersViewModel>();
                    contentType = ContentType.Folder;
                    break;
                case "Config":
                    var configView = _serviceProvider.GetRequiredService<ConfigView>();
                    viewModel = (ViewModelBase)configView.DataContext;
                    contentType = ContentType.Config;
                    break;
                case "Detail":
                    viewModel = _serviceProvider.GetRequiredService<DetailsViewModel>();
                    contentType = ContentType.Detail;
                    break;
                default:
                    viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                    contentType = ContentType.Home;
                    break;
            }

            CurrentContentType = contentType;
            CurrentPage = viewModel;

            UpdateSortingControlsVisibility(viewModel);
            LoadSortStateForContentType(contentType);

            // if Library or Details and in case of Details if is not playlist and not nowplaying, show buttons
            ShowViewTypeButtons = CurrentPage is LibraryViewModel || (CurrentPage is DetailsViewModel detailsVm && 
                detailsVm.ContentType != ContentType.Playlist && 
                detailsVm.ContentType != ContentType.NowPlaying) ;

            // Save state after navigation
            await _stateManager.SaveCurrentState();
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsExpanded = !IsExpanded;
        }

        public async Task NavigateToDetails(ContentType type, object data)
        {
            var detailsViewModel = _serviceProvider.GetRequiredService<DetailsViewModel>();
            await Navigate(ContentType.Detail.ToString());
            await detailsViewModel.Initialize(type, data);
            UpdateSortingControlsVisibility(detailsViewModel);
        }

        private void UpdateSortingControlsVisibility(ViewModelBase page)
        {
            if (page is DetailsViewModel detailsVM)
            {
                // Hide sorting controls for non-sortable content types
                ShowSortingControls = detailsVM.ContentType != ContentType.NowPlaying &&
                                     detailsVM.ContentType != ContentType.Playlist;
            }
            else if (page is LibraryViewModel libraryVM)
            {
                // Always show sorting controls for LibraryViewModel
                ShowSortingControls = true;
            }
            else
            {
                // For other view types - show sort controls for collection views
                ShowSortingControls = page is ArtistsViewModel ||
                                     page is AlbumsViewModel ||
                                     page is PlaylistsViewModel ||
                                     page is GenresViewModel ||
                                     page is FoldersViewModel;
            }
        }

        public async Task NavigateToSearch(SearchViewModel searchViewModel)
        {
            _searchViewModel.ShowSearchFlyout = false;
            CurrentPage = searchViewModel;
            CurrentContentType = ContentType.Search;
            UpdateSortingControlsVisibility(CurrentPage);
            ShowViewTypeButtons = false;
            ResetReorder();
        }

        public void ResetReorder()
        {
            var _detailsVM = _serviceProvider.GetService<DetailsViewModel>();
            if (_detailsVM != null && _detailsVM.IsReorderMode)
            {
                _detailsVM.CancelReorder();
            }
        }

        private async Task HandleProfileUpdate(ProfileUpdateMessage message)
        {
            if (message.UpdatedProfile != null)
            {
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
            else if (CurrentPage is DetailsViewModel detailsVM)
            {
                detailsVM.CurrentViewType = parsedViewType;
            }

        }

        private void UpdateAvailableSortTypes(ContentType contentType)
        {
            // Default - show all options
            ShowNameSortOption = true;
            ShowArtistSortOption = true;
            ShowAlbumSortOption = true;
            ShowDurationSortOption = true;
            ShowGenreSortOption = true;
            ShowReleaseDateSortOption = true;

            // Adjust based on content type
            switch (contentType)
            {
                case ContentType.Library:
                case ContentType.Detail:
                    // Show all options for Library and Detail (unless playlist/nowplaying)
                    break;

                case ContentType.Artist:
                case ContentType.Album:
                case ContentType.Genre:
                case ContentType.Folder:
                case ContentType.Playlist:
                    // Show only Name and Duration for collection views
                    ShowArtistSortOption = false;
                    ShowAlbumSortOption = false;
                    ShowGenreSortOption = false;
                    ShowReleaseDateSortOption = false;
                    break;

                // These types have no sorting controls at all
                case ContentType.NowPlaying:
                case ContentType.Home:
                case ContentType.Search:
                case ContentType.Config:
                    break;
            }
        }


        private void UpdateSortState(ContentType contentType, SortType type, SortDirection direction, bool isUserInitiated = false)
        {
            _sortingStates[contentType] = new ViewSortingState
            {
                SortType = type,
                SortDirection = direction
            };

            // Update current state
            SelectedSortType = type;
            SortDirection = direction;

            // Update UI text
            SelectedSortTypeText = SortTypeMap.FirstOrDefault(x => x.Value.Type == type).Value.Display ?? "Name";
            SelectedSortDirectionText = direction == SortDirection.Ascending ? "A-Z" : "Z-A";

            // Notify sort update - specify if user initiated
            _messenger.Send(new SortUpdateMessage(type, direction, isUserInitiated));
        }

        [RelayCommand]
        public void SetSortDirection(string direction)
        {
            var newDirection = direction == "A-Z" ? SortDirection.Ascending : SortDirection.Descending;
            // This is user-initiated sort change
            UpdateSortState(CurrentContentType, SelectedSortType, newDirection, true);
        }

        [RelayCommand]
        public void SetSortType(string sortType)
        {
            if (SortTypeMap.TryGetValue(sortType.ToLower(), out var mapping))
            {
                // This is user-initiated sort change
                UpdateSortState(CurrentContentType, mapping.Type, SortDirection, true);
            }
        }

        public void SetSortingStates(Dictionary<string, ViewSortingState> states)
        {
            // Clear current states
            _sortingStates.Clear();

            // Convert string-based dictionary to ContentType-based
            foreach (var pair in states)
            {
                if (Enum.TryParse(pair.Key, true, out ContentType contentType))
                {
                    _sortingStates[contentType] = pair.Value;
                }
                else if (pair.Key == "library")
                {
                    _sortingStates[ContentType.Library] = pair.Value;
                }
                else if (pair.Key == "details")
                {
                    _sortingStates[ContentType.Detail] = pair.Value;
                }
            }
        }


        public Dictionary<string, ViewSortingState> GetSortingStates()
        {
            // Update current content type's state
            UpdateSortState(CurrentContentType, SelectedSortType, SortDirection);

            // Convert to string-based dictionary for state manager
            var stringBasedStates = new Dictionary<string, ViewSortingState>();

            foreach (var pair in _sortingStates)
            {
                stringBasedStates[pair.Key.ToString().ToLower()] = pair.Value;
            }

            return stringBasedStates;
        }

        public void InitializeTempSortSettings()
        {
            TempSortType = SelectedSortType;
            TempSortDirection = SortDirection;

            // Update checked states
            UpdateTempSortTypeCheckedStates();
            UpdateTempSortDirectionCheckedStates();
        }

        private void UpdateTempSortTypeCheckedStates()
        {
            IsTempSortTypeName = TempSortType == SortType.Name;
            IsTempSortTypeArtist = TempSortType == SortType.Artist;
            IsTempSortTypeAlbum = TempSortType == SortType.Album;
            IsTempSortTypeDuration = TempSortType == SortType.Duration;
            IsTempSortTypeGenre = TempSortType == SortType.Genre;
            IsTempSortTypeReleaseDate = TempSortType == SortType.ReleaseDate;
        }

        private void UpdateTempSortDirectionCheckedStates()
        {
            IsTempSortDirectionAscending = TempSortDirection == SortDirection.Ascending;
            IsTempSortDirectionDescending = TempSortDirection == SortDirection.Descending;
        }

        // Apply button command with event to close popup
        [RelayCommand]
        private void ApplySort()
        {
            // Only apply if something changed
            if (TempSortType != SelectedSortType || TempSortDirection != SortDirection)
            {
                // Update UI elements
                SelectedSortType = TempSortType;
                SortDirection = TempSortDirection;

                // Update display text
                SelectedSortTypeText = SortTypeMap.FirstOrDefault(x => x.Value.Type == TempSortType).Value.Display ?? "Name";
                SelectedSortDirectionText = TempSortDirection == SortDirection.Ascending ? "A-Z" : "Z-A";

                // Update arrow transform
                SortIconTransform = TempSortDirection == SortDirection.Ascending
                    ? new RotateTransform(180)  // Up arrow
                    : new RotateTransform(0);   // Down arrow

                // Update state
                _sortingStates[CurrentContentType] = new ViewSortingState
                {
                    SortType = TempSortType,
                    SortDirection = TempSortDirection
                };

                // Send message with user-initiated flag
                _messenger.Send(new SortUpdateMessage(TempSortType, TempSortDirection, true));
            }

            // No need to raise event - the button click handler takes care of closing the popup
        }


        public void LoadSortStateForContentType(ContentType contentType)
        {
            // Skip loading for non-sortable content types
            if (contentType == ContentType.NowPlaying ||
                contentType == ContentType.Home ||
                contentType == ContentType.Search ||
                contentType == ContentType.Config)
            {
                UpdateAvailableSortTypes(contentType);
                return;
            }

            if (_sortingStates.TryGetValue(contentType, out var state))
            {
                // Update available sort types first
                UpdateAvailableSortTypes(contentType);

                // Apply the saved state WITHOUT sending user-initiated flag
                // Update internal state variables directly
                SelectedSortType = state.SortType;
                SortDirection = state.SortDirection;

                // Update UI text
                SelectedSortTypeText = SortTypeMap.FirstOrDefault(x => x.Value.Type == state.SortType).Value.Display ?? "Name";
                SelectedSortDirectionText = state.SortDirection == SortDirection.Ascending ? "A-Z" : "Z-A";

                // Send a non-user-initiated message
                _messenger.Send(new SortUpdateMessage(state.SortType, state.SortDirection, false));

                
            }
            else
            {
                // If no state exists, create default based on content type
                SortType defaultSortType = SortType.Name;
                SortDirection defaultDirection = SortDirection.Ascending;

                UpdateAvailableSortTypes(contentType);

                // Update internal state WITHOUT triggering user-initiated events
                SelectedSortType = defaultSortType;
                SortDirection = defaultDirection;
                SelectedSortTypeText = "Name";
                SelectedSortDirectionText = "A-Z";

                // Send a non-user-initiated message
                _messenger.Send(new SortUpdateMessage(defaultSortType, defaultDirection, false));

            }
        }

        [RelayCommand]
        private void SetTempSortType(string sortType)
        {
            if (SortTypeMap.TryGetValue(sortType.ToLower(), out var mapping))
            {
                TempSortType = mapping.Type;
                UpdateTempSortTypeCheckedStates();
            }
        }

        // Command to update temporary sort direction
        [RelayCommand]
        private void SetTempSortDirection(string direction)
        {
            TempSortDirection = direction.Equals("Ascending", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Ascending
                : SortDirection.Descending;

            UpdateTempSortDirectionCheckedStates();
        }

        [RelayCommand]
        public async Task OpenProfileDialog()
        {
            ResetReorder(); // Reset to prevent issues with reorder

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

        [RelayCommand]
        private async Task ToggleSearchBox()
        {
            ResetReorder(); // Reset to prevent issues with reorder

            if (!ShowSearchBox)
            {
                // Showing the textbox
                ShowSearchBox = true;
                SearchBoxOpacity = 1;
                SearchBoxWidth = 200;
                UpdateSearchIcon();
            }
            else
            {
                // Hiding the textbox - animate first, then hide
                SearchBoxWidth = 0;
                SearchBoxOpacity = 0;
                UpdateSearchIcon();
                await Task.Delay(200); // Wait for animation
                ShowSearchBox = false;
            }

            if (!ShowSearchBox)
            {
                UpdateSearchIcon();
                SearchViewModel.SearchQuery = string.Empty;
                SearchViewModel.ShowSearchFlyout = false;
            }
        }

        private void UpdateSearchIcon()
        {
            // Use the icons directly from the resources
            SearchToggleIcon = ShowSearchBox ?
                (StreamGeometry)App.Current.FindResource("CloseIcon") :
                (StreamGeometry)App.Current.FindResource("SearchIcon");
        }

        [RelayCommand]
        private void ExecuteSaveReorder()
        {
            SaveReorderAction?.Invoke();
        }

        [RelayCommand]
        private void ExecuteCancelReorder()
        {
            CancelReorderAction?.Invoke();
        }

        public async void StartBackgroundScan()
        {
            var directories = await _directoryService.GetAllDirectories();
            await _profileManager.InitializeAsync();
            await Task.Run(() => _directoryScannerService.ScanDirectoriesAsync(directories, _profileManager.CurrentProfile.ProfileID));
        }
    }
}