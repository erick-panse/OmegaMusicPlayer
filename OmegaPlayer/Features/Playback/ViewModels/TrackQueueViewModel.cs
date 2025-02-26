using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OmegaPlayer.Core.Services;
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
        public bool IsShuffleOperation { get; }

        public TrackQueueUpdateMessage(TrackDisplayModel currentTrack, ObservableCollection<TrackDisplayModel> queue, int currentTrackIndex, bool isShuffleOperation = false)
        {
            CurrentTrack = currentTrack;
            Queue = queue;
            CurrentTrackIndex = currentTrackIndex;
            IsShuffleOperation = isShuffleOperation;
        }
    }
    public enum RepeatMode
    {
        None,
        All,
        One
    }

    public partial class TrackQueueViewModel : ViewModelBase
    {
        private readonly QueueService _queueService;
        private readonly TracksService _tracksService;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly ProfileManager _profileManager;
        private readonly PlayHistoryService _playHistoryService;
        private readonly TrackStatsService _trackStatsService;
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

        [ObservableProperty]
        private RepeatMode _repeatMode = RepeatMode.None;

        private int _currentTrackIndex;

        private ObservableCollection<TrackDisplayModel> _originalQueue;
        private int _originalTrackIndex;
        private bool _isShuffled;

        public bool IsShuffled
        {
            get => _isShuffled;
            set => SetProperty(ref _isShuffled, value);
        }

        public TrackQueueViewModel(
            QueueService queueService, 
            TracksService tracksService,
            TrackDisplayService trackDisplayService, 
            ProfileManager profileManager,
            PlayHistoryService playHistoryService,
            TrackStatsService trackStatsService,
            IMessenger messenger)
        {
            _queueService = queueService;
            _tracksService = tracksService;
            _trackDisplayService = trackDisplayService;
            _profileManager = profileManager;
            _playHistoryService = playHistoryService;
            _trackStatsService = trackStatsService;
            _messenger = messenger;
            LoadLastPlayedQueue();
        }


        public async Task LoadLastPlayedQueue()
        {
            try
            {
                var queueState = await _queueService.GetCurrentQueueState(await GetCurrentProfileId());
                if (queueState == null || !queueState.Tracks.Any()) return;

                // Set queue state
                IsShuffled = queueState.IsShuffled;
                RepeatMode = Enum.Parse<RepeatMode>(queueState.RepeatMode);

                // If shuffled, use OriginalOrder, otherwise use TrackOrder
                var orderedTracks = queueState.IsShuffled
                    ? queueState.Tracks.OrderBy(t => t.OriginalOrder).ToList()
                    : queueState.Tracks.OrderBy(t => t.TrackOrder).ToList();

                var tracks = await _trackDisplayService.GetTrackDisplaysFromQueue(orderedTracks);
                if (tracks == null || !tracks.Any()) return;

                // Set up queue
                NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(tracks);

                // Store original positions for shuffle/unshuffle
                for (int i = 0; i < tracks.Count; i++)
                {
                    tracks[i].NowPlayingPosition = queueState.Tracks[i].OriginalOrder;
                }

                var currentTrack = tracks.ElementAtOrDefault(queueState.CurrentQueue.CurrentTrackOrder);
                if (currentTrack != null)
                {
                    CurrentTrack = currentTrack;
                    _currentTrackIndex = queueState.CurrentQueue.CurrentTrackOrder;
                }

                UpdateDurations();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading queue: {ex.Message}");
            }
        }


        private async Task<int> GetCurrentProfileId()
        {
            await _profileManager.InitializeAsync();
            return _profileManager.CurrentProfile.ProfileID;
        }

        private async void SetCurrentTrack(int trackIndex)
        {
            CurrentTrack = trackIndex >= 0 && trackIndex < NowPlayingQueue.Count
            ? NowPlayingQueue[trackIndex]
            : null;

            await SaveCurrentTrack();
            UpdateDurations();

            SaveCurrentQueueState().ConfigureAwait(false);
        }
        public int GetCurrentTrackIndex()
        {
            return _currentTrackIndex;
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
                SaveCurrentQueueState().ConfigureAwait(false);
                return;
            }

            // Insert after current track without changing current track or index
            var insertIndex = _currentTrackIndex + 1;
            foreach (var track in tracksToAdd.Reverse()) // Reverse to maintain order when inserting
            {
                NowPlayingQueue.Insert(insertIndex, track);
            }
            SaveCurrentQueueState().ConfigureAwait(false);

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
                SaveCurrentQueueState().ConfigureAwait(false);
                return;
            }

            // Add tracks to end without changing current track or index
            foreach (var track in tracks)
            {
                NowPlayingQueue.Add(track);
            }
            SaveCurrentQueueState().ConfigureAwait(false);
        }

        public int GetNextTrack()
        {
            if (!NowPlayingQueue.Any()) return -1;
            // RepeatMode.None is handled in trackcontrol
            switch (RepeatMode)
            {
                case RepeatMode.All:
                    return _currentTrackIndex + 1 >= NowPlayingQueue.Count ? 0 : _currentTrackIndex + 1;
                default:
                    return _currentTrackIndex + 1 < NowPlayingQueue.Count ? _currentTrackIndex + 1 : -1;
            }
        }

        public int GetPreviousTrack()
        {
            if (!NowPlayingQueue.Any()) return -1;
            // RepeatMode.None is handled in trackcontrol
            switch (RepeatMode)
            {
                case RepeatMode.All:
                    return _currentTrackIndex - 1 >= 0 ? _currentTrackIndex - 1 : NowPlayingQueue.Count - 1;
                default:
                    return _currentTrackIndex - 1 >= 0 ? _currentTrackIndex - 1 : -1;
            }
        }

        public async Task UpdateCurrentTrackIndex(int newIndex)
        {
            if (_currentTrackIndex < 0) return;

            _currentTrackIndex = newIndex;
            CurrentTrack = NowPlayingQueue[_currentTrackIndex];
            UpdateDurations();
            await SaveCurrentTrack();
        }

        public void UpdateQueueAndTrack(ObservableCollection<TrackDisplayModel> newQueue, int newIndex)
        {
            NowPlayingQueue = newQueue;
            CurrentTrack = NowPlayingQueue[newIndex];
            _currentTrackIndex = newIndex;

        }

        public void ToggleShuffle()
        {
            IsShuffled = !IsShuffled;
            if (NowPlayingQueue.Any())
            {
                if (IsShuffled)
                {
                    // Store original order
                    for (int i = 0; i < NowPlayingQueue.Count; i++)
                    {
                        NowPlayingQueue[i].NowPlayingPosition = i;
                    }

                    // Get segments before and after current track
                    var beforeCurrent = NowPlayingQueue.Take(_currentTrackIndex).ToList();
                    var afterCurrent = NowPlayingQueue.Skip(_currentTrackIndex + 1).ToList();
                    var currentTrack = NowPlayingQueue[_currentTrackIndex];

                    // Shuffle both segments independently
                    var shuffledBefore = beforeCurrent.OrderBy(x => Guid.NewGuid()).ToList();
                    var shuffledAfter = afterCurrent.OrderBy(x => Guid.NewGuid()).ToList();

                    // Reconstruct queue maintaining current track position
                    NowPlayingQueue.Clear();
                    foreach (var track in shuffledBefore)
                    {
                        NowPlayingQueue.Add(track);
                    }
                    NowPlayingQueue.Add(currentTrack); // Current track stays in same position
                    foreach (var track in shuffledAfter)
                    {
                        NowPlayingQueue.Add(track);
                    }
                }
                else
                {
                    // Restore original order using NowPlayingPosition
                    var currentTrack = NowPlayingQueue[_currentTrackIndex];
                    var orderedTracks = NowPlayingQueue
                        .OrderBy(t => t.NowPlayingPosition)
                        .ToList();
                    NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(orderedTracks);
                    _currentTrackIndex = orderedTracks.IndexOf(currentTrack);
                }

                SaveCurrentQueueState().ConfigureAwait(false);
            }
        }


        public void ToggleRepeatMode()
        {
            RepeatMode = RepeatMode switch
            {
                RepeatMode.None => RepeatMode.All,
                RepeatMode.All => RepeatMode.One,
                RepeatMode.One => RepeatMode.None,
                _ => RepeatMode.None
            };
        }

        // Method to save only the current track
        public async Task SaveCurrentTrack()
        {
            if (CurrentTrack != null)
            {
                await _queueService.SaveCurrentTrackAsync(CurrentQueueId, _currentTrackIndex, await GetCurrentProfileId());

                await IncrementPlayCount();

                await _playHistoryService.AddToHistory(CurrentTrack);
            }
        }

        public async Task IncrementPlayCount()
        {
            if (CurrentTrack != null)
            {
                CurrentTrack.PlayCount++;
                await _trackStatsService.IncrementPlayCount(CurrentTrack.TrackID, CurrentTrack.PlayCount);
            }
        }


        // Method to save only the NowPlayingQueue (excluding the CurrentTrack)
        public async Task SaveCurrentQueueState()
        {
            if (!NowPlayingQueue.Any()) return;

            try
            {
                await _queueService.SaveCurrentQueueState(
                    await GetCurrentProfileId(),
                    NowPlayingQueue.ToList(),
                    _currentTrackIndex,
                    IsShuffled,
                    RepeatMode.ToString()
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving queue state: {ex.Message}");
            }
        }

        public async Task SaveReorderedQueue(List<TrackDisplayModel> reorderedTracks, int newCurrentTrackIndex)
        {
            try
            {
                // Update NowPlayingIndex position to match the new order
                _currentTrackIndex = newCurrentTrackIndex;

                // Update queue positions to match the new order
                for (int i = 0; i < reorderedTracks.Count; i++)
                {
                    reorderedTracks[i].NowPlayingPosition = i;
                }

                // Update the observable collection
                NowPlayingQueue.Clear();
                foreach (var track in reorderedTracks)
                {
                    NowPlayingQueue.Add(track);
                }

                // Use QueueService to save the new state
                await _queueService.SaveCurrentQueueState(
                    await GetCurrentProfileId(),
                    reorderedTracks,
                    _currentTrackIndex,
                    IsShuffled,
                    RepeatMode.ToString()
                );

                // Notify any subscribers of the queue update
                // Send queue update message with isShuffleOperation = true to prevent track restart
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex, isShuffleOperation: true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving reordered queue: {ex.Message}");
                throw;
            }
        }

        public void UpdateDurations()
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
