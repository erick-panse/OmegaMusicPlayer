using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.Models;
using OmegaPlayer.Features.Playback.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playback.ViewModels
{
    public class TrackQueueUpdateMessage
    {
        public TrackDisplayModel CurrentTrack { get; }
        public ObservableCollection<TrackDisplayModel> Queue { get; }
        public int CurrentTrackIndex { get; }

        public TrackQueueUpdateMessage(TrackDisplayModel currentTrack, ObservableCollection<TrackDisplayModel> queue, int currentTrackIndex)
        {
            CurrentTrack = currentTrack;
            Queue = queue;
            CurrentTrackIndex = currentTrackIndex;
        }
    }

    public partial class TrackQueueViewModel : ViewModelBase
    {
        private readonly QueueService _queueService;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _nowPlayingQueue = new ObservableCollection<TrackDisplayModel>();

        [ObservableProperty]
        private TrackDisplayModel _currentTrack;

        [ObservableProperty]
        private int _currentQueueId;

        [ObservableProperty]
        private TimeSpan _remainingDuration;

        [ObservableProperty]
        private TimeSpan _totalDuration;

        private int _currentTrackIndex;

        public TrackQueueViewModel(QueueService queueService, TrackDisplayService trackDisplayService, IMessenger messenger)
        {
            _queueService = queueService;
            _trackDisplayService = trackDisplayService;
            _messenger = messenger;
            LoadLastPlayedQueue();
        }


        public async Task LoadLastPlayedQueue()
        {
            try
            {
                var result = await _queueService.GetCurrentQueueByProfileId(GetCurrentProfileId());

                if (result == null || result.Tracks == null || result.Tracks.Count == 0)
                {
                    Console.WriteLine("No last played queue found for the profile.");
                    return;
                }

                CurrentQueueId = result.CurrentQueueByProfile.QueueID;
                var trackDisplays = await _trackDisplayService.GetTrackDisplaysFromQueue(result.Tracks, GetCurrentProfileId());

                NowPlayingQueue.Clear(); // Clear existing queue first
                foreach (var track in trackDisplays)
                {
                    NowPlayingQueue.Add(track); // Add each TrackDisplay to the ObservableCollection
                }

                _currentTrackIndex = result.CurrentQueueByProfile.CurrentTrackOrder;
                // Set the last played track as the current track
                SetCurrentTrack(_currentTrackIndex);

                await UpdateDurations();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw it, so the app doesn't crash
                Console.WriteLine($"Error loading last played queue: {ex.Message}");
            }
        }

        private int GetCurrentProfileId()
        {
            // Return the current profile ID based on the logged-in user
            return 2; // This should be dynamically set, placeholder for now
        }

        private async void SetCurrentTrack(int trackIndex)
        {
            CurrentTrack = trackIndex >= 0 && trackIndex < NowPlayingQueue.Count
            ? NowPlayingQueue[trackIndex]
            : null;

            await SaveCurrentTrack();
            await UpdateDurations();
            //await SaveNowPlayingQueue(); - FIX later
        }
        private HashSet<int> _processedTrackIds = new();
        // Method to play a specific track and add others before/after it to the queue
        public void PlayThisTrack(TrackDisplayModel track, ObservableCollection<TrackDisplayModel> allTracks)
        {
            _processedTrackIds.Clear();

            // Preserve the exact position of tracks
            var newQueue = new ObservableCollection<TrackDisplayModel>();
            var targetIndex = -1;

            for (int i = 0; i < allTracks.Count; i++)
            {
                var currentTrack = allTracks[i];
                newQueue.Add(currentTrack);

                // Use reference equality to find exact track instance
                if (ReferenceEquals(currentTrack, track))
                {
                    targetIndex = i;
                }
            }

            if (targetIndex == -1)
            {
                return;
            }

            // Update state atomically
            NowPlayingQueue = newQueue;
            _currentTrackIndex = targetIndex;
            SetCurrentTrack(_currentTrackIndex);

            // Notify subscribers
            _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));
            UpdateDurations();

        }

        public void AddToPlayNext(ObservableCollection<TrackDisplayModel> tracksToAdd)
        {
            if (tracksToAdd == null || !tracksToAdd.Any()) return;

            // If queue is empty or no track is playing, start fresh
            if (!NowPlayingQueue.Any() || CurrentTrack == null)
            {
                foreach (var track in tracksToAdd)
                {
                    NowPlayingQueue.Add(track);
                }
                _currentTrackIndex = 0;
                CurrentTrack = NowPlayingQueue[_currentTrackIndex];
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));
                return;
            }

            // Insert after current track without changing current track or index
            var insertIndex = _currentTrackIndex + 1;
            foreach (var track in tracksToAdd.Reverse()) // Reverse to maintain order when inserting
            {
                NowPlayingQueue.Insert(insertIndex, track);
            }

            UpdateDurations();
        }
        public void AddTrackToQueue(ObservableCollection<TrackDisplayModel> tracks)
        {
            if (tracks == null) return;

            // If queue is empty or no track is playing, start fresh
            if (!NowPlayingQueue.Any() || CurrentTrack == null)
            {
                foreach (var track in tracks)
                {
                    NowPlayingQueue.Add(track);
                }
                _currentTrackIndex = 0;
                CurrentTrack = NowPlayingQueue[_currentTrackIndex];
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));
                return;
            }

            // Add tracks to end without changing current track or index
            foreach (var track in tracks)
            {
                NowPlayingQueue.Add(track);
            }
        }

        public int GetNextTrack()
        {
            return NowPlayingQueue.Count - 1 >= _currentTrackIndex + 1 ? _currentTrackIndex + 1 : -1;
        }

        public int GetPreviousTrack()
        {
            return _currentTrackIndex - 1 >= 0 ? _currentTrackIndex - 1 : -1;
        }

        public void UpdateCurrentTrackIndex(int newIndex)
        {
            if (_currentTrackIndex < 0) return;

            _currentTrackIndex = newIndex;
            CurrentTrack = NowPlayingQueue[_currentTrackIndex];
            UpdateDurations();
        }

        public void UpdateQueueAndTrack(ObservableCollection<TrackDisplayModel> newQueue, int newIndex)
        {
            NowPlayingQueue = newQueue;
            CurrentTrack = NowPlayingQueue[newIndex];
            _currentTrackIndex = newIndex;

        }

        // Method to save only the current track
        public async Task SaveCurrentTrack()
        {
            if (CurrentTrack != null)
            {
                await _queueService.SaveCurrentTrackAsync(CurrentQueueId, _currentTrackIndex, GetCurrentProfileId());
            }
        }

        // Method to save only the NowPlayingQueue (excluding the CurrentTrack)
        public async Task SaveNowPlayingQueue()
        {
            if (NowPlayingQueue.Any())
            {
                var queueTracks = NowPlayingQueue.Select((track, index) => new QueueTracks
                {
                    QueueID = CurrentQueueId,
                    TrackID = track.TrackID,
                    TrackOrder = index
                }).ToList();

                await _queueService.SaveNowPlayingQueueAsync(CurrentQueueId, queueTracks, GetCurrentProfileId());
            }
        }

        public async Task UpdateDurations()
        {
            TotalDuration = TimeSpan.FromTicks(NowPlayingQueue.Sum(t => t.Duration.Ticks));

            if (CurrentTrack != null)
            {
                var currentIndex = NowPlayingQueue.IndexOf(CurrentTrack);
                RemainingDuration = TimeSpan.FromTicks(
                    NowPlayingQueue.Skip(currentIndex).Sum(t => t.Duration.Ticks));
            }
        }


    }

}
