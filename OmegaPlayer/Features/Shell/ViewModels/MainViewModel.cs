using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using System;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Home.ViewModels;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Threading.Tasks;
using OmegaPlayer.Core.Navigation.Services;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly DirectoriesService _directoryService;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.List;

        [ObservableProperty]
        private bool _showSortingControls;

        // Sort Direction Properties
        [ObservableProperty]
        private string _sortDirection = "Custom";

        [ObservableProperty]
        private bool _isSortDirectionCustom = true;

        [ObservableProperty]
        private bool _isSortDirectionAscending;

        [ObservableProperty]
        private bool _isSortDirectionDescending;

        // Sort Type Properties
        [ObservableProperty]
        private string _sortType = "Title";

        [ObservableProperty]
        private bool _isSortTypeTitle = true;

        [ObservableProperty]
        private bool _isSortTypeArtist;

        [ObservableProperty]
        private bool _isSortTypeAlbum;

        [ObservableProperty]
        private bool _showViewTypeButtons = false;

        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                SetProperty(ref _currentPage, value);
                // Show sorting controls only for views that display track listings
                ShowSortingControls = value is LibraryViewModel;
            }
        }

        public TrackControlViewModel TrackControlViewModel { get; }

        public MainViewModel(
            DirectoryScannerService directoryScannerService,
            DirectoriesService directoryService,
            TrackControlViewModel trackControlViewModel,
            IServiceProvider serviceProvider,
            INavigationService navigationService)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            TrackControlViewModel = trackControlViewModel;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;

            // Set initial page
            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();

            StartBackgroundScan();

            navigationService.NavigationRequested += async (s, e) => await NavigateToDetails(e.Type, e.Data);
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            // Clear current view state in navigation service
            _navigationService.ClearCurrentView();
            ViewModelBase viewModel;
            switch (destination)
            {
                case "Home":
                    viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                    break;
                case "Library":
                    viewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
                    break;
                case "Artists":
                    viewModel = _serviceProvider.GetRequiredService<ArtistViewModel>();
                    break;
                case "Albums":
                    viewModel = _serviceProvider.GetRequiredService<AlbumViewModel>();
                    break;
                case "Playlists":
                    viewModel = _serviceProvider.GetRequiredService<PlaylistViewModel>();
                    break;
                case "Genres":
                    viewModel = _serviceProvider.GetRequiredService<GenreViewModel>();
                    break;
                case "Folders":
                    viewModel = _serviceProvider.GetRequiredService<FolderViewModel>();
                    break;
                default:
                    throw new ArgumentException($"Unknown destination: {destination}");
            }
            CurrentPage = viewModel;
            ShowViewTypeButtons = CurrentPage is LibraryViewModel;
        }

        [RelayCommand]
        private void ToggleNavigation()
        {
            IsExpanded = !IsExpanded;
        }

        [RelayCommand]
        private void SetSortDirection(string direction)
        {
            SortDirection = direction;
            IsSortDirectionCustom = direction == "Custom";
            IsSortDirectionAscending = direction == "Ascending";
            IsSortDirectionDescending = direction == "Descending";
            UpdateSorting();
        }

        public async Task NavigateToDetails(ContentType type, object data)
        {
            var detailsViewModel = _serviceProvider.GetRequiredService<DetailsViewModel>();
            await detailsViewModel.Initialize(type, data);
            CurrentPage = detailsViewModel;
            ShowViewTypeButtons = CurrentPage is DetailsViewModel;
        }



        [RelayCommand]
        private void SetSortType(string type)
        {
            SortType = type;
            IsSortTypeTitle = type == "Title";
            IsSortTypeArtist = type == "Artist";
            IsSortTypeAlbum = type == "Album";
            UpdateSorting();
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

        private void UpdateSorting()
        {
            if (CurrentPage is LibraryViewModel gridVM)
            {
                //gridVM.UpdateSorting(SortDirection, SortType);
            }
        }

        //partial void OnSelectedSortDirectionChanged(string value)
        //{
        //    if (CurrentPage is LibraryViewModel gridVM)
        //    {
        //        // Update grid view sorting
        //        //gridVM.UpdateSorting(value, SelectedSortType);
        //    }
        //    else if (CurrentPage is ListViewModel listVM)
        //    {
        //        // Update list view sorting
        //        //listVM.UpdateSorting(value, SelectedSortType);
        //    }
        //}

        //partial void OnSelectedSortTypeChanged(string value)
        //{
        //    if (CurrentPage is LibraryViewModel gridVM)
        //    {
        //        // Update grid view sorting
        //        //gridVM.UpdateSorting(SelectedSortDirection, value);
        //    }
        //    else if (CurrentPage is ListViewModel listVM)
        //    {
        //        // Update list view sorting
        //        //listVM.UpdateSorting(SelectedSortDirection, value);
        //    }
        //}

        public async void StartBackgroundScan()
        {
            var directories = await _directoryService.GetAllDirectories();
            await Task.Run(() => _directoryScannerService.ScanDirectoriesAsync(directories));
        }
    }
}