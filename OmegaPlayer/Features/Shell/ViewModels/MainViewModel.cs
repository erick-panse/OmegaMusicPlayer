using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.Navigation.Services;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Configuration.Views;
using OmegaPlayer.Features.Home.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Profile.Views;
using OmegaPlayer.Features.Search.ViewModels;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static OmegaPlayer.Core.Navigation.Services.NavigationService;

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
        private readonly LocalizationService _localizationService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        private readonly Stack<NavigationHistoryItem> _previousPages = new();
        private readonly Stack<NavigationHistoryItem> _nextPages = new();
        private bool _isNavigatingFromHistory = false;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private ContentType _currentContentType = ContentType.Home;

        [ObservableProperty]
        private bool _showSortingControls;

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
        private bool _isTempSortTypePlayCount;

        [ObservableProperty]
        private bool _isTempSortTypeFileCreated;

        [ObservableProperty]
        private bool _isTempSortTypeFileModified;

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
        private bool _showPlayCountSortOption = true;

        [ObservableProperty]
        private bool _showFileCreatedSortOption = true;

        [ObservableProperty]
        private bool _showFileModifiedSortOption = true;

        [ObservableProperty]
        private bool _isLibraryScanInProgress = false;

        [ObservableProperty]
        private string _libraryScanText = string.Empty;

        [ObservableProperty]
        private string _currentView = "home";

        [ObservableProperty]
        private bool _canNavigateBack = false;

        [ObservableProperty]
        private bool _canNavigateForward = false;

        public ObservableCollection<string> AvailableSortTypes { get; } = new ObservableCollection<string>();

        private static readonly Dictionary<string, (SortType Type, string Display)> SortTypeMap = new();

        private Dictionary<ContentType, ViewSortingState> _sortingStates = new();

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
            LocalizationService localizationService,
            AllTracksRepository allTracksRepository,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
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
            _localizationService = localizationService;
            _allTracksRepository = allTracksRepository;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            // Set initial page
            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();

            // Initialize the sort type map with localized display text
            InitializeSortTypeMap();
            InitializeProfilePhoto();
            StartBackgroundScan();
            InitializeAudioMonitoring();
            UpdateSearchIcon();

            // Register for language changes
            _messenger.Register<LanguageChangedMessage>(this, (r, m) =>
            {
                // Update localized text when language changes
                InitializeSortTypeMap();

                // Update current display text
                UpdateSortDisplayText();
            });

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

            navigationService.NavigationRequested += async (s, e) => await NavigateToDetails(e.Type, e.Data);
            _messenger.Register<NavigationRequestMessage>(this, (r, m) => NavigateToDetails(m.ContentType, m.Data));

            // Register for library scan messages
            _messenger.Register<LibraryScanStartedMessage>(this, (r, m) => HandleLibraryScanStarted(m));
            _messenger.Register<LibraryScanCompletedMessage>(this, (r, m) => HandleLibraryScanCompleted(m));

            // Register for profile update messages
            _messenger.Register<ProfileChangedMessage>(this, (r, m) => InitializeProfilePhoto());
            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileUpdate(m));

            // Register for profile update messages
            _messenger.Register<ShowLyricsMessage>(this, async (r, m) => ToggleLyicsPage());
        }

        private async void InitializeAudioMonitoring()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Get profile config
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    var config = await _profileConfigService.GetProfileConfig(profile.ProfileID);

                    // Enable/disable dynamic pause based on profile settings
                    _audioMonitorService.EnableDynamicPause(config.DynamicPause);
                },
                "Initializing audio monitoring",
                ErrorSeverity.NonCritical
            );
        }

        private void HandleLibraryScanStarted(LibraryScanStartedMessage message)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    IsLibraryScanInProgress = true;
                    LibraryScanText = _localizationService["ScanningLibrary"];
                },
                "Handling library scan started",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task HandleLibraryScanCompleted(LibraryScanCompletedMessage message)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Hide loading animation
                    IsLibraryScanInProgress = false;
                    LibraryScanText = string.Empty;

                    // Show notification with scan results
                    var scanSummary = $"Library scan completed: {message.ProcessedFiles} files processed, " +
                                        $"{message.AddedFiles} added, {message.UpdatedFiles} updated, " +
                                        $"{message.RemovedFiles} removed or blacklisted";

                    // Only Show notification to the user if there were tracks added or updated
                    bool showNotification = message.AddedFiles > 0 || message.UpdatedFiles > 0 || message.RemovedFiles != 0 ? true : false;

                    _errorHandlingService.LogInfo(
                        "Library Scan Completed",
                        scanSummary,
                        showNotification);

                    // Only refresh if there were tracks added or updated
                    if (showNotification)
                    {
                        // Refresh AllTracksRepository data using injected dependency
                        if (_allTracksRepository != null)
                        {
                            // Invalidate caches to force reload
                            _allTracksRepository.InvalidateAllCaches();

                            // Trigger reload
                            await _allTracksRepository.LoadTracks(forceRefresh: true);
                        }
                    }
                },
                "Handling library scan completed",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task Navigation(string destination, ContentType type = ContentType.Home, object data = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Add to navigation history before navigating
                    if (destination == "details")
                    {
                        AddToNavigationHistory(destination, type, data);
                    }
                    else
                    {
                        AddToNavigationHistory(destination, CurrentContentType);
                    }
                    //clear selected items in their respective views
                    Type? pageType = CurrentPage?.GetType();
                    if (pageType != null)
                    {
                        var clearMethod = pageType.GetMethod("ClearSelection") ?? pageType.GetMethod("DeselectAllTracks");
                        clearMethod?.Invoke(CurrentPage, null);
                    }

                    ResetReorder();

                    // Update current view
                    CurrentView = destination.ToLower();

                    // Clear current view state in navigation service
                    _navigationService.ClearCurrentView();
                    ViewModelBase viewModel;
                    ContentType contentType;

                    // Hide TrackControl indicators
                    TrackControlViewModel.IsLyricsOpen = false;
                    TrackControlViewModel.IsNowPlayingOpen = false;

                    // Get the appropriate view model based on destination
                    switch (CurrentView)
                    {
                        case "home":
                            viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                            contentType = ContentType.Home;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            ((HomeViewModel)viewModel).Initialize();
                            break;
                        case "library":
                            viewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
                            contentType = ContentType.Library;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((LibraryViewModel)viewModel).Initialize(false);
                            break;
                        case "artists":
                            viewModel = _serviceProvider.GetRequiredService<ArtistsViewModel>();
                            contentType = ContentType.Artist;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((ArtistsViewModel)viewModel).Initialize();
                            break;
                        case "albums":
                            viewModel = _serviceProvider.GetRequiredService<AlbumsViewModel>();
                            contentType = ContentType.Album;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((AlbumsViewModel)viewModel).Initialize();
                            break;
                        case "playlists":
                            viewModel = _serviceProvider.GetRequiredService<PlaylistsViewModel>();
                            contentType = ContentType.Playlist;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((PlaylistsViewModel)viewModel).Initialize();
                            break;
                        case "genres":
                            viewModel = _serviceProvider.GetRequiredService<GenresViewModel>();
                            contentType = ContentType.Genre;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((GenresViewModel)viewModel).Initialize();
                            break;
                        case "folders":
                            viewModel = _serviceProvider.GetRequiredService<FoldersViewModel>();
                            contentType = ContentType.Folder;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _ = ((FoldersViewModel)viewModel).Initialize();
                            break;
                        case "config":
                            var configView = _serviceProvider.GetRequiredService<ConfigView>();
                            viewModel = (ViewModelBase)configView.DataContext;
                            contentType = ContentType.Config;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            break;
                        case "details":
                            viewModel = _serviceProvider.GetRequiredService<DetailsViewModel>();
                            contentType = ContentType.Details;
                            _navigationService.NotifyBeforeNavigationChange(type, data);
                            _ = ((DetailsViewModel)viewModel).Initialize(type, data);
                            break;
                        case "lyrics":
                            viewModel = _serviceProvider.GetRequiredService<LyricsViewModel>();
                            contentType = ContentType.Lyrics;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            TrackControlViewModel.IsLyricsOpen = true; // Show TrackControl indicator
                            ((LyricsViewModel)viewModel).InitializeProperties();
                            break;
                        case "search":
                            viewModel = _serviceProvider.GetRequiredService<SearchViewModel>();
                            contentType = ContentType.Search;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            _searchViewModel.ShowSearchFlyout = false;
                            break;
                        default:
                            viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                            contentType = ContentType.Home;
                            _navigationService.NotifyBeforeNavigationChange(contentType);
                            ((HomeViewModel)viewModel).Initialize();
                            break;
                    }

                    CurrentContentType = contentType;
                    CurrentPage = viewModel;

                    UpdateSortingControlsVisibility(viewModel);
                    LoadSortStateForContentType(contentType);

                    // if Library or Details and in case of Details if is not playlist and not nowplaying, show buttons
                    ShowViewTypeButtons = CurrentPage is LibraryViewModel || (CurrentPage is DetailsViewModel detailsVm &&
                        detailsVm.ContentType != ContentType.Playlist &&
                        detailsVm.ContentType != ContentType.NowPlaying);

                    // Save state after navigation
                    await _stateManager.SaveCurrentState();

                    // Update navigation buttons after successful navigation
                    UpdateNavigationButtons();
                },
                $"Navigating to {destination}",
                ErrorSeverity.NonCritical
            );
        }

        public async Task NavigateToDetails(ContentType type, object data)
        {
            await Navigation(ContentType.Details.ToString(), type, data);
        }

        [RelayCommand]
        public async Task Navigate(string destination)
        {
            await Navigation(destination);
        }

        public async void ToggleLyicsPage()
        {
            if (CurrentPage is LyricsViewModel lyricsVm)
                await NavigateBack();
            else
                await Navigate("Lyrics");
        }

        [RelayCommand]
        public async Task NavigateBack()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!CanNavigateBack || _previousPages.Count == 0) return;

                    _isNavigatingFromHistory = true;

                    try
                    {
                        // Save current page to next stack
                        var currentHistoryItem = new NavigationHistoryItem
                        {
                            Destination = CurrentView,
                            ContentType = CurrentContentType,
                            Data = GetCurrentNavigationData(),
                            ViewModel = CurrentPage
                        };

                        // For details views, store the specific content type being shown
                        if (CurrentView == "details" && CurrentPage is DetailsViewModel detailsVM)
                        {
                            currentHistoryItem.DetailsContentType = detailsVM.ContentType;
                        }

                        _nextPages.Push(currentHistoryItem);

                        // Prevent navigating back to the same destination
                        if (_previousPages.Count > 0)
                        {
                            var lastItem = _previousPages.Peek();

                            // Compare destination
                            bool sameDestination = string.Equals(lastItem.Destination, currentHistoryItem.Destination, StringComparison.OrdinalIgnoreCase);

                            if (sameDestination)
                            {
                                if (currentHistoryItem.Destination == "details")
                                {
                                    // For details views, compare both the details content type and data
                                    bool sameDetailsType = lastItem.DetailsContentType == currentHistoryItem.DetailsContentType;
                                    bool sameData = AreNavigationDataEqual(lastItem.Data, currentHistoryItem.Data);
                                    if (sameDetailsType && sameData)
                                    {
                                        _previousPages.Pop(); // Remove duplicate entry
                                    }
                                }
                                else
                                {
                                    // For regular views, compare content type
                                    if (lastItem.ContentType == currentHistoryItem.ContentType)
                                    {
                                        _previousPages.Pop(); // Remove duplicate entry
                                    }
                                }
                            }
                        }

                        // Get previous page
                        var previousItem = _previousPages.Pop();

                        // Navigate to previous page
                        if (previousItem.Destination == "details")
                        {
                            // For details views, use the stored DetailsContentType and data
                            await NavigateToDetails(previousItem.DetailsContentType ?? ContentType.Artist, previousItem.Data);
                        }
                        else
                        {
                            await Navigate(previousItem.Destination);
                        }

                        UpdateNavigationButtons();
                    }
                    finally
                    {
                        _isNavigatingFromHistory = false;
                    }
                },
                "Navigating back in history",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        public async Task NavigateForward()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!CanNavigateForward || _nextPages.Count == 0) return;

                    _isNavigatingFromHistory = true;

                    try
                    {
                        // Save current page to previous stack
                        var currentHistoryItem = new NavigationHistoryItem
                        {
                            Destination = CurrentView,
                            ContentType = CurrentContentType,
                            Data = GetCurrentNavigationData(),
                            ViewModel = CurrentPage
                        };

                        // For details views, store the specific content type being shown
                        if (CurrentView == "details" && CurrentPage is DetailsViewModel detailsVM)
                        {
                            currentHistoryItem.DetailsContentType = detailsVM.ContentType;
                        }

                        _previousPages.Push(currentHistoryItem);

                        // Get next page
                        var nextItem = _nextPages.Pop();

                        // Navigate to next page
                        if (nextItem.Destination == "details")
                        {
                            // For details views, use the stored DetailsContentType and data
                            await NavigateToDetails(nextItem.DetailsContentType ?? ContentType.Artist, nextItem.Data);
                        }
                        else
                        {
                            await Navigate(nextItem.Destination);
                        }

                        UpdateNavigationButtons();
                    }
                    finally
                    {
                        _isNavigatingFromHistory = false;
                    }
                },
                "Navigating forward in history",
                ErrorSeverity.NonCritical
            );
        }

        private object GetCurrentNavigationData()
        {
            // Return the current navigation data based on content type
            if (CurrentPage is DetailsViewModel detailsVM)
            {
                return detailsVM.CurrentData;
            }
            return null;
        }

        private void UpdateNavigationButtons()
        {
            CanNavigateBack = _previousPages.Count > 0 && !(_previousPages.Count == 1 && CurrentPage is HomeViewModel);
            CanNavigateForward = _nextPages.Count > 0;
        }

        // Replace the existing AddToNavigationHistory method in MainViewModel
        private void AddToNavigationHistory(string destination, ContentType contentType, object data = null)
        {
            if (_isNavigatingFromHistory) return; // Don't add to history when navigating from history

            // Add current page to history before navigating
            if (!string.IsNullOrEmpty(CurrentView))
            {
                var historyItem = new NavigationHistoryItem
                {
                    Destination = CurrentView,
                    ContentType = CurrentContentType,
                    Data = GetCurrentNavigationData(),
                    ViewModel = CurrentPage
                };

                // For details views, store the specific content type being shown
                if (CurrentView == "details" && CurrentPage is DetailsViewModel detailsVM)
                {
                    historyItem.DetailsContentType = detailsVM.ContentType;
                }

                // Check if this is a duplicate of the last entry
                bool isDuplicate = false;
                if (_previousPages.Count > 0)
                {
                    var lastItem = _previousPages.Peek();

                    // Compare destination
                    bool sameDestination = string.Equals(lastItem.Destination, historyItem.Destination, StringComparison.OrdinalIgnoreCase);

                    if (sameDestination)
                    {
                        if (historyItem.Destination == "details")
                        {
                            // For details views, compare both the details content type and data
                            bool sameDetailsType = lastItem.DetailsContentType == historyItem.DetailsContentType;
                            bool sameData = AreNavigationDataEqual(lastItem.Data, historyItem.Data);
                            isDuplicate = sameDetailsType && sameData;
                        }
                        else
                        {
                            // For regular views, just compare content type
                            isDuplicate = lastItem.ContentType == historyItem.ContentType;
                        }
                    }
                }

                // Only add to history if it's not a duplicate
                if (!isDuplicate)
                {
                    _previousPages.Push(historyItem);

                    // Clear next pages when navigating normally (not from history)
                    _nextPages.Clear();
                }
            }

            UpdateNavigationButtons();
        }

        // Add this helper method to compare navigation data for detail pages
        private bool AreNavigationDataEqual(object data1, object data2)
        {
            // If both are null, they're equal
            if (data1 == null && data2 == null) return true;

            // If one is null and the other isn't, they're not equal
            if (data1 == null || data2 == null) return false;

            // If they're the same reference, they're equal
            if (ReferenceEquals(data1, data2)) return true;

            // Compare based on type
            return (data1, data2) switch
            {
                // Artist comparison
                (ArtistDisplayModel artist1, ArtistDisplayModel artist2) => artist1.ArtistID == artist2.ArtistID,

                // Album comparison  
                (AlbumDisplayModel album1, AlbumDisplayModel album2) => album1.AlbumID == album2.AlbumID,

                // Genre comparison
                (GenreDisplayModel genre1, GenreDisplayModel genre2) =>
                    string.Equals(genre1.Name, genre2.Name, StringComparison.OrdinalIgnoreCase),

                // Playlist comparison
                (PlaylistDisplayModel playlist1, PlaylistDisplayModel playlist2) => playlist1.PlaylistID == playlist2.PlaylistID,

                // Folder comparison (assuming it has a path or identifier)
                (FolderDisplayModel folder1, FolderDisplayModel folder2) =>
                    string.Equals(folder1.FolderPath, folder2.FolderPath, StringComparison.OrdinalIgnoreCase),

                // NowPlayingInfo comparison
                (NowPlayingInfo nowPlaying1, NowPlayingInfo nowPlaying2) =>
                    nowPlaying1.CurrentTrack?.TrackID == nowPlaying2.CurrentTrack?.TrackID,

                // Default: use Equals method
                _ => data1.Equals(data2)
            };
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsExpanded = !IsExpanded;
        }

        // Initialize the sort type map with localized display text
        private void InitializeSortTypeMap()
        {
            SortTypeMap.Clear();
            SortTypeMap.Add("name", (SortType.Name, _localizationService["Name"]));
            SortTypeMap.Add("artist", (SortType.Artist, _localizationService["Artist"]));
            SortTypeMap.Add("album", (SortType.Album, _localizationService["Album"]));
            SortTypeMap.Add("duration", (SortType.Duration, _localizationService["Duration"]));
            SortTypeMap.Add("genre", (SortType.Genre, _localizationService["Genre"]));
            SortTypeMap.Add("playcount", (SortType.PlayCount, _localizationService["PlayCount"]));
            SortTypeMap.Add("filecreated", (SortType.FileCreated, _localizationService["FileCreated"]));
            SortTypeMap.Add("filemodified", (SortType.FileModified, _localizationService["FileModified"]));
        }

        // Update the display text based on current sort settings
        private void UpdateSortDisplayText()
        {
            // Update the sort type text
            SelectedSortTypeText = SortTypeMap.FirstOrDefault(x => x.Value.Type == SelectedSortType).Value.Display ?? _localizationService["Name"];

            // Update the sort direction text
            SelectedSortDirectionText = SortDirection == SortDirection.Ascending ?
                _localizationService["Ascending"] :
                _localizationService["Descending"];

            // Notify property changed to update UI
            OnPropertyChanged(nameof(SelectedSortTypeText));
            OnPropertyChanged(nameof(SelectedSortDirectionText));
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
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (message.UpdatedProfile == null) return;

                    // Refresh AllTracksRepository data using injected dependency
                    if (_allTracksRepository != null)
                    {
                        // Invalidate caches to force reload
                        _allTracksRepository.InvalidateAllCaches();

                        // Trigger reload and forget
                        _ = _allTracksRepository.LoadTracks(forceRefresh: true);
                    }

                    if (message.UpdatedProfile.PhotoID > 0)
                    {
                        var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                        CurrentProfilePhoto = await profileService.LoadProfilePhotoAsync(message.UpdatedProfile.PhotoID, "low", true);
                    }
                    else
                    {
                        CurrentProfilePhoto = null;
                    }
                },
                "Updating profile information",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async void InitializeProfilePhoto()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Delay to let update be processed
                    await Task.Delay(100);

                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile.PhotoID > 0)
                    {
                        var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                        CurrentProfilePhoto = await profileService.LoadProfilePhotoAsync(profile.PhotoID, "low", true);
                    }
                },
                "Loading profile photo",
                ErrorSeverity.NonCritical,
                false
            );
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
            ShowPlayCountSortOption = true;
            ShowFileCreatedSortOption = true;
            ShowFileModifiedSortOption = true;

            // Adjust based on content type
            switch (contentType)
            {
                case ContentType.Library:
                case ContentType.Details:
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
                    ShowPlayCountSortOption = false;
                    ShowFileCreatedSortOption = false;
                    ShowFileModifiedSortOption = false;
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

            // Update UI text with localized versions
            SelectedSortTypeText = SortTypeMap.FirstOrDefault(x => x.Value.Type == type).Value.Display ?? _localizationService["Name"];
            SelectedSortDirectionText = direction == SortDirection.Ascending ?
                _localizationService["Ascending"] :
                _localizationService["Descending"];

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
                    _sortingStates[ContentType.Details] = pair.Value;
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
            IsTempSortTypePlayCount = TempSortType == SortType.PlayCount;
            IsTempSortTypeFileCreated = TempSortType == SortType.FileCreated;
            IsTempSortTypeFileModified = TempSortType == SortType.FileModified;
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

                // Update arrow transform
                SortIconTransform = state.SortDirection == SortDirection.Ascending
                    ? new RotateTransform(180)  // Up arrow
                    : new RotateTransform(0);   // Down arrow

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
            if (SortTypeMap.TryGetValue(sortType.ToLower().Trim(), out var mapping))
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
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                "Opening profile dialog",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task ToggleSearchBox()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                "Toggling search box",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void UpdateSearchIcon()
        {
            // Use the icons directly from the resources
            SearchToggleIcon = ShowSearchBox ?
                (StreamGeometry)App.Current.FindResource("CloseIcon") :
                (StreamGeometry)App.Current.FindResource("SearchIconV2");
        }

        public async void StartBackgroundScan()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var directories = await _directoryService.GetAllDirectories();

                    var profile = await _profileManager.GetCurrentProfileAsync();

                    // Run the scan in a separate task to prevent it from affecting the UI
                    _ = Task.Run(async () =>
                    {
                        await _errorHandlingService.SafeExecuteAsync(
                            async () => await _directoryScannerService.ScanDirectoriesAsync(
                                directories,
                                profile.ProfileID
                            ),
                            "Scanning music directories in background",
                            ErrorSeverity.NonCritical,
                            false // Don't show notification for background process
                        );
                    });
                },
                "Starting background music scan",
                ErrorSeverity.NonCritical,
                false
            );
        }

        [RelayCommand]
        private void TestError(ErrorSeverity severity = ErrorSeverity.NonCritical)
        {
            try
            {
                // Select different error types based on severity to demonstrate different behaviors
                switch (severity)
                {
                    case ErrorSeverity.Critical:
                        // Simulate a critical error (like database connection failure)
                        throw new InvalidOperationException("This is a simulated critical error. Application functionality might be limited.");

                    case ErrorSeverity.Playback:
                        // Simulate a playback error
                        throw new System.IO.IOException("This is a simulated playback error. The track could not be played.");

                    case ErrorSeverity.NonCritical:
                        // Simulate a non-critical error (like metadata loading failure)
                        throw new FormatException("This is a simulated non-critical error. Some information might be missing.");

                    case ErrorSeverity.Info:
                        // Just log an informational message
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Information Message",
                            "This is a test information message. Everything is working correctly.",
                            null,
                            true);
                        return;
                }
            }
            catch (Exception ex)
            {
                // Log the error - this will show a toast notification
                _errorHandlingService.LogError(
                    severity,
                    $"Test {severity} Error",
                    ex.Message,
                    ex,
                    true);
            }
        }
    }
}