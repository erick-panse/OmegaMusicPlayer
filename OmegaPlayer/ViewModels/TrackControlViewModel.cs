using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using NAudio.Wave;
using OmegaPlayer.Models;
using OmegaPlayer.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Repositories;
using System;

namespace OmegaPlayer.ViewModels
{
    public partial class TrackControlViewModel : ViewModelBase
    {

        private readonly TrackDisplayService _trackDService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;

        public List<TrackDisplayModel> AllTracks { get; set; }

        private IWavePlayer _waveOut;
        private AudioFileReader _audioFileReader;

        public TrackControlViewModel(TrackDisplayService trackDService, TrackQueueViewModel trackQueueViewModel, AllTracksRepository allTracksRepository)
        {
            _trackDService = trackDService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            AllTracks = _allTracksRepository.AllTracks;
            LoadAllTracks();

            InitializeWaveOut(); // Ensure _waveOut is initialized

            // Register for messages from TrackQueueViewModel
            //WeakReferenceMessenger.Default.Register<CurrentTrackChangedMessage>(this, (r, m) =>
            //{
            //    CurrentTrack = m.Value;
            //});

            //WeakReferenceMessenger.Default.Register<NowPlayingQueueChangedMessage>(this, (r, m) =>
            //{
            //    NowPlayingQueue = m.Value;
            //});
        }

        private PlaybackState _isPlaying = PlaybackState.Stopped;

        //[ObservableProperty]
        //private ObservableCollection<TrackDisplayModel> _nowPlayingQueue = new ObservableCollection<TrackDisplayModel>();

        //[ObservableProperty]
        //private TrackDisplayModel _currentTrack;

        [ObservableProperty]
        private double _trackDuration; // Total duration of the track
        [ObservableProperty]
        private double _trackPosition; // Current playback position

        [ObservableProperty]
        private string _currentTitle;

        [ObservableProperty]
        private List<Artists> _currentArtists;

        [ObservableProperty]
        private string _currentAlbumTitle;

        [ObservableProperty]
        private Bitmap _trackImage;


        private async void LoadAllTracks()
        {
            try
            {
                AllTracks = await _trackDService.GetAllTracksWithMetadata(2);// 2 is a dummy profileid made for testing
            }
            catch
            {
                ShowMessageBox("Error when trying to fetch all tracks");
            }
        }

        [RelayCommand]
        public void PlayPause()
        {


            if (_waveOut == null)
            {
                InitializeWaveOut();
            }
            if (_audioFileReader == null || _isPlaying == PlaybackState.Paused || _isPlaying == PlaybackState.Stopped)
            {
                var currentTrack = GetCurrentTrack();
                if (_audioFileReader == null && currentTrack.FilePath != null)
                {
                    StopPlayback();
                    _audioFileReader = new AudioFileReader(currentTrack.FilePath);
                    _waveOut.Init(_audioFileReader);
                }

                _waveOut.Play();
                _isPlaying = _waveOut.PlaybackState;
            }
            else
            {
                _waveOut.Pause();
                _isPlaying = _waveOut.PlaybackState;
            }

        }

        public void PlayCurrentTrack(TrackDisplayModel track, ObservableCollection<TrackDisplayModel> allTracks)
        {
            if (track == null || allTracks == null) { return; }

            _trackQueueViewModel.PlayTrack(track, allTracks);

            InitializeWaveOut();// Stop any previous playback and Initialize playback device

            // Load the new track's file
            _audioFileReader = new AudioFileReader(track.FilePath); // FilePath is assumed to be the path to the audio file


            // Hook up the audio file to the player
            _waveOut.Init(_audioFileReader);

            // Play the track
            _waveOut.Play();
            _isPlaying = _waveOut.PlaybackState;


        }

        private void InitializeWaveOut()
        {
            // Initialize playback device (e.g., WaveOutEvent for default output)
            StopPlayback();
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += HandlePlaybackStopped;
        }

        public void StopPlayback()
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
        }

        private void HandlePlaybackStopped(object sender, StoppedEventArgs e)
        {
            TimeSpan timeRemaining = _audioFileReader.TotalTime - _audioFileReader.CurrentTime;
            double secondsRemaining = timeRemaining.TotalSeconds;
            if (secondsRemaining < 1)
            {
                PlayNextTrack();
            }
            else
            {
                //StopPlayback(); // or loop, or do nothing when queue finishes
            }
        }

        private ObservableCollection<TrackDisplayModel> GetCurrentQueue()
        {
            return new ObservableCollection<TrackDisplayModel>(_trackQueueViewModel.NowPlayingQueue);
        }
        private TrackDisplayModel GetCurrentTrack()
        {
            return _trackQueueViewModel.CurrentTrack;
        }

        [RelayCommand]
        public void AdvanceBySeconds()// "int seconds" use variable for customable seconds skip, for now fixed to 5
        {
            //_audioFileReader.CurrentTime = _audioFileReader.TotalTime.Subtract(TimeSpan.FromSeconds(3)); // for testing, advances to 3 sec before the end of the track
            if (_audioFileReader != null)
            {
                var newPosition = _audioFileReader.CurrentTime.Add(TimeSpan.FromSeconds(5));
                _audioFileReader.CurrentTime = newPosition < _audioFileReader.TotalTime ? newPosition : _audioFileReader.TotalTime;
            }
        }
        [RelayCommand]
        public void RegressBySeconds() // "int seconds" use variable for customable seconds skip, for now fixed to 5
        {
            if (_audioFileReader != null)
            {
                var newPosition = _audioFileReader.CurrentTime.Subtract(TimeSpan.FromSeconds(5));
                _audioFileReader.CurrentTime = newPosition > TimeSpan.Zero ? newPosition : TimeSpan.Zero;
            }
        }


        [RelayCommand]
        private void ShowNowPlaying()
        {
            // Logic to display the Now Playing queue
            // Example: Open a new window that shows the _trackQueueViewModel.NowPlayingQueue
        }

        [RelayCommand]
        public void OpenImage()
        {
        }
        [RelayCommand]
        public void OpenArtist()
        {
        }
        [RelayCommand]
        public void OpenAlbum()
        {
        }
        [RelayCommand]
        public void Shuffle()
        {
        }
        [RelayCommand]
        public void PlayNextTrack()
        {
            var nextTrack = _trackQueueViewModel.GetNextTrack();
            if (nextTrack != null)
            {
                var currentQueue = GetCurrentQueue();
                PlayCurrentTrack(nextTrack, currentQueue);
            }
        }
        [RelayCommand]
        public void PlayPreviousTrack()
        {
            // Check if the current track is playing and the current time is more than 5 seconds
            if (_audioFileReader == null) { return; }

            var previousTrack = _trackQueueViewModel.GetPreviousTrack();
            if (_audioFileReader.CurrentTime.TotalSeconds > 5 || previousTrack == null)
            {
                // if less thean 5 sec has run or has not found previous track Restart the current track by setting CurrentTime to 0
                _audioFileReader.CurrentTime = TimeSpan.Zero;
            }
            else
            {
                var currentQueue = GetCurrentQueue();
                PlayCurrentTrack(previousTrack, currentQueue);
            }


        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync(); // shows custom messages
        }
    }

}
