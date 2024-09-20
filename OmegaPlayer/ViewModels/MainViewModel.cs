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

        public MainViewModel(DirectoryScannerService directoryScannerService, DirectoriesService directoryService, IServiceProvider serviceProvider)
        {
            _directoryScannerService = directoryScannerService;
            _directoryService = directoryService;
            _serviceProvider = serviceProvider;

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
            //if (directories.Count == 0) { }
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

        [ObservableProperty]
        private NAudio.Wave.PlaybackState _isPlaying = NAudio.Wave.PlaybackState.Stopped;

        [RelayCommand]
        public void PlayPauseAction()
        {
            if (outputDevice == null)
            {
                DisposeWave();
                outputDevice = new WaveOutEvent();
            }
            if (audioFile == null || (IsPlaying == NAudio.Wave.PlaybackState.Stopped))
            {
                if (audioFile == null)
                {
                    DisposeWave();
                    audioFile = new AudioFileReader(@"C:\Users\Erick\Music\GHOST DATA - Deadly Flourish(MP3_160K).mp3");
                    outputDevice.Init(audioFile);
                }

                outputDevice.Play();
                IsPlaying = outputDevice.PlaybackState;
            }
            else
            {
                outputDevice.Stop();
                IsPlaying = outputDevice.PlaybackState;
            }
        }

        private void OnClosing(CancelEventArgs e)
        {
            DisposeWave();
        }

        private void DisposeWave()
        {
            if (outputDevice != null)
            {
                if (IsPlaying == NAudio.Wave.PlaybackState.Playing) { outputDevice.Stop(); }
            }
            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }
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
