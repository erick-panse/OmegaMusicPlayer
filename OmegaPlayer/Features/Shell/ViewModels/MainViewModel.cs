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

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly DirectoriesService _directoryService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private bool _isExpanded = true;

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

        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                SetProperty(ref _currentPage, value);
                // Show sorting controls only for views that display track listings
                ShowSortingControls = value is LibraryViewModel || value is ListViewModel;
            }
        }

        public TrackControlViewModel TrackControlViewModel { get; }

        public MainViewModel(
            DirectoryScannerService directoryScannerService,
            DirectoriesService directoryService,
            IServiceProvider serviceProvider,
            TrackControlViewModel trackControlViewModel)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            _serviceProvider = serviceProvider;
            TrackControlViewModel = trackControlViewModel;

            // Set initial page
            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();

            StartBackgroundScan();
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            ViewModelBase viewModel;
            switch (destination)
            {
                case "Home":
                    viewModel = _serviceProvider.GetRequiredService<HomeViewModel>();
                    break;
                case "Library":
                    viewModel = _serviceProvider.GetRequiredService<LibraryViewModel>();
                    break;
                case "List":
                    viewModel = _serviceProvider.GetRequiredService<ListViewModel>();
                    break;
                default:
                    throw new ArgumentException($"Unknown destination: {destination}");
            }
            CurrentPage = viewModel;
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

        [RelayCommand]
        private void SetSortType(string type)
        {
            SortType = type;
            IsSortTypeTitle = type == "Title";
            IsSortTypeArtist = type == "Artist";
            IsSortTypeAlbum = type == "Album";
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            if (CurrentPage is LibraryViewModel gridVM)
            {
                //gridVM.UpdateSorting(SortDirection, SortType);
            }
            else if (CurrentPage is ListViewModel listVM)
            {
                //listVM.UpdateSorting(SortDirection, SortType);
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