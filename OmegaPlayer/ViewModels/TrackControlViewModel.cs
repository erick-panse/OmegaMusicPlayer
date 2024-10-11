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
using System.Timers;
using System.Diagnostics;

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
        [ObservableProperty]
        private float _trackVolume;

        public TrackControlViewModel(TrackDisplayService trackDService, TrackQueueViewModel trackQueueViewModel, AllTracksRepository allTracksRepository)
        {
            _trackDService = trackDService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            AllTracks = _allTracksRepository.AllTracks;
            LoadAllTracks();

            InitializeWaveOut(); // Ensure _waveOut is initialized

            _timer = new Timer(250); // Initialize but do not start the timer
            _timer.Elapsed += TimerElapsed;
        }
        [ObservableProperty]
        public PlaybackState _isPlaying = PlaybackState.Stopped;
        private readonly Timer _timer;
        private bool _isSeeking = false;
        private bool _isDragging = false;

        [ObservableProperty]
        private TimeSpan _trackDuration; // Total duration of the track
        [ObservableProperty]
        private TimeSpan _trackPosition; // Current playback position

        [ObservableProperty]
        private double trackTitleMaxWidth;

        [ObservableProperty]
        private string _currentTitle;

        [ObservableProperty]
        private List<Artists> _currentArtists;

        [ObservableProperty]
        private string _currentAlbumTitle;

        [ObservableProperty]
        private Bitmap _currentTrackImage;

        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_audioFileReader != null && _isSeeking == false)
            {
                TrackPosition = _audioFileReader.CurrentTime;
            }
        }
        public void Seek(double newPosition)
        {
            if (_audioFileReader != null && newPosition >= 0 && newPosition <= TrackDuration.TotalSeconds)
            {
                _isSeeking = true;
                TrackPosition = TimeSpan.FromSeconds(newPosition);
            }
        }
        public void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }

        public void StopSeeking()
        {
            _isSeeking = false;
            if (_audioFileReader == null) return;
            _audioFileReader.CurrentTime = TrackPosition.TotalSeconds <= 0 ? TimeSpan.Zero : TrackPosition;

        }
        public void ChangeVolume(double newVolume)
        {
            // Volume should be between 0.0f (mute) and 1.0f (max)
            if (newVolume < 0 || _audioFileReader == null) return;
            TrackVolume = (float)newVolume;
            SetVolume();
        }

        public void SetVolume()
        {
            // Volume should be between 0.0f (mute) and 1.0f (max)
            if (TrackVolume < 0 || _audioFileReader == null) return;

            _audioFileReader.Volume = TrackVolume;
        }

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
            var currentTrack = GetCurrentTrack();
            if (_audioFileReader == null && currentTrack == null) { return; }

            if (_waveOut == null) { InitializeWaveOut(); }

            if (_isPlaying != PlaybackState.Playing)
            {
                if (_audioFileReader == null && currentTrack != null)//(_audioFileReader == null && _audioFileReader.Filename != currentTrack.Filename)
                {
                    _waveOut.Pause();
                    _audioFileReader = new AudioFileReader(currentTrack.FilePath);
                    SetVolume();
                    _waveOut.Init(_audioFileReader);
                    UpdateTrackInfo();
                }

                _waveOut.Play();
                _isPlaying = _waveOut.PlaybackState;
                _timer.Start(); // Start the timer when playback begins
            }
            else
            {
                _waveOut.Pause();
                _isPlaying = _waveOut.PlaybackState;
                _timer.Stop(); // Stop the timer when paused
            }

        }

        public async void PlayCurrentTrack(TrackDisplayModel track, ObservableCollection<TrackDisplayModel> allTracks)
        {
            if (track == null || allTracks == null) { return; }

            _trackQueueViewModel.PlayTrack(track, allTracks);

            InitializeWaveOut();// Stop any previous playback and Initialize playback device

            // Load the new track's file
            _audioFileReader = new AudioFileReader(track.FilePath); // FilePath is assumed to be the path to the audio file
            SetVolume();


            // Hook up the audio file to the player
            _waveOut.Init(_audioFileReader);

            // Play the track
            _waveOut.Play();
            _isPlaying = _waveOut.PlaybackState;
            _timer.Start(); // Start the timer when a new track starts playing

            await _trackQueueViewModel.SaveNowPlayingQueue();
            UpdateTrackInfo();
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
                //_timer.Stop();
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
            ShowMessageBox($"Opening artist: {CurrentArtists}");
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

        private async void UpdateTrackInfo()
        {
            var track = GetCurrentTrack();

            if (track == null) { return; }

            //if (_audioFileReader.FileName == track.FilePath) { return; }

            CurrentTitle = track.Title;
            CurrentArtists = track.Artists;
            CurrentAlbumTitle = track.AlbumTitle;

            track.Artists.Last().IsLastArtist = false;// Hides the Comma of the last Track
            await _trackDService.LoadHighResThumbnailAsync(track);// Load track Thumbnail

            CurrentTrackImage = track.Thumbnail;
            TrackDuration = _audioFileReader.TotalTime;
            TrackPosition = TimeSpan.Zero;
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync(); // shows custom messages
        }
    }

}
