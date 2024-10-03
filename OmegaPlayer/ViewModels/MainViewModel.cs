using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using NAudio.Wave;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using OmegaPlayer.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaPlayer.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly DirectoryScannerService _directoryScannerService;
        private readonly DirectoriesService _directoryService;
        private readonly IServiceProvider _serviceProvider;
        public TrackControlViewModel TrackControlViewModel { get; }

        public MainViewModel(DirectoryScannerService directoryScannerService, DirectoriesService directoryService, IServiceProvider serviceProvider, TrackControlViewModel trackControlViewModel)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            _serviceProvider = serviceProvider;
            TrackControlViewModel = trackControlViewModel;

            StartBackgroundScan();

            CurrentPage = _serviceProvider.GetRequiredService<HomeViewModel>();
            Items = new()
            {
            new ListItemTemplate(typeof(HomeViewModel), "MusicRegular"),
            new ListItemTemplate(typeof(GridViewModel), "ArtistRegular"),
            new ListItemTemplate(typeof(ListViewModel), "TabDesktopRegular"),
            };
        }

        private ViewModelBase _currentPage;
        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public ObservableCollection<ListItemTemplate> Items { get; }

        partial void OnSelectedListItemChanged(ListItemTemplate? value)
        {
            if (value == null) return;
            var instance = (ViewModelBase)_serviceProvider.GetService(value.ModelType)!;
            if (instance is null) return;
            CurrentPage = instance;
        }


        public async void StartBackgroundScan()
        {
            var directories = await _directoryService.GetAllDirectories();
            // se nao tiver nenhum diretorio pedir um
            await Task.Run(() => _directoryScannerService.ScanDirectoriesAsync(directories));
        }


        [ObservableProperty]
        private bool _isPaneOpen = true;

        [RelayCommand]
        public void TriggerPane()
        {
            IsPaneOpen = !IsPaneOpen;
        }
        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;

        [ObservableProperty]
        private ListItemTemplate? _selectedListItem;

        [RelayCommand]
        private void OnClosing(CancelEventArgs e)
        {
            TrackControlViewModel.StopPlayback();
        }


    }


    public class ListItemTemplate
    {
        public ListItemTemplate(Type type, string iconKey)
        {
            ModelType = type;
            Label = type.Name.Replace("ViewModel", "");

            Application.Current!.TryFindResource(iconKey, out var res);
            ListItemIcon = (StreamGeometry)res!;
        }
        public string Label { get; }
        public Type ModelType { get; } // The type of ViewModel to be resolved via DI
        //public Image ItemImage { get; }
        public StreamGeometry ListItemIcon { get; }
    }
}
