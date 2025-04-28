using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
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
        private readonly TrackDisplayService _trackDisplayService;
        private readonly ProfileManager _profileManager;
        private readonly PlayHistoryService _playHistoryService;
        private readonly TrackStatsService _trackStatsService;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

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
            TrackDisplayService trackDisplayService,
            ProfileManager profileManager,
            PlayHistoryService playHistoryService,
            TrackStatsService trackStatsService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _queueService = queueService;
            _trackDisplayService = trackDisplayService;
            _profileManager = profileManager;
            _playHistoryService = playHistoryService;
            _trackStatsService = trackStatsService;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            LoadLastPlayedQueue();
        }

        public async Task LoadLastPlayedQueue(int retryCount = 3)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                var profileId = await GetCurrentProfileId();
                if (profileId < 0)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Failed to load queue",
                        "Invalid profile ID when attempting to load queue.",
                        null,
                        true);
                    return;
                }

                var queueState = await _queueService.GetCurrentQueueState(profileId);
                if (queueState == null)
                {
                    _errorHandlingService.LogInfo(
                        "No queue state found for current profile",
                        $"Profile ID: {profileId}");
                    return;
                }

                if (queueState.Tracks == null || !queueState.Tracks.Any())
                {
                    _errorHandlingService.LogInfo(
                        "Queue exists but contains no tracks",
                        $"Profile ID: {profileId}, Queue ID: {queueState.CurrentQueue?.QueueID ?? -1}");
                    return;
                }

                try
                {
                    // Set queue state
                    IsShuffled = queueState.IsShuffled;
                    RepeatMode = Enum.TryParse<RepeatMode>(queueState.RepeatMode, true, out var repeatMode)
                        ? repeatMode
                        : RepeatMode.None;

                    // If shuffled, use OriginalOrder, otherwise use TrackOrder
                    var orderedTracks = queueState.IsShuffled
                        ? queueState.Tracks.OrderBy(t => t.OriginalOrder).ToList()
                        : queueState.Tracks.OrderBy(t => t.TrackOrder).ToList();

                    var tracks = await _trackDisplayService.GetTrackDisplaysFromQueue(orderedTracks);
                    if (tracks == null || !tracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to load track display data",
                            "Could not retrieve track display information for queue tracks.",
                            null,
                            false);
                        return;
                    }

                    // Set up queue
                    NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(tracks);

                    // Ensure current track index is valid
                    int currentTrackIndex = queueState.CurrentQueue.CurrentTrackOrder;
                    if (currentTrackIndex < 0 || currentTrackIndex >= tracks.Count)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid current track index",
                            $"Current track index ({currentTrackIndex}) is out of range for queue with {tracks.Count} tracks. Resetting to 0.",
                            null,
                            false);
                        currentTrackIndex = 0;
                    }

                    var currentTrack = tracks.ElementAtOrDefault(currentTrackIndex);
                    if (currentTrack != null)
                    {
                        CurrentTrack = currentTrack;
                        _currentTrackIndex = currentTrackIndex;
                    }
                    else if (tracks.Any())
                    {
                        // Fallback to first track if current track is invalid
                        CurrentTrack = tracks.First();
                        _currentTrackIndex = 0;
                    }

                    UpdateDurations();
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Playback,
                        "Error processing queue data",
                        "An unexpected error occurred while processing queue data.",
                        ex,
                        true);

                    // If we have retries left, wait a moment and try again
                    if (retryCount > 0)
                    {
                        await Task.Delay(500); // Wait 500ms before retrying
                        await LoadLastPlayedQueue(retryCount - 1);
                    }
                }
            },
            "Loading last played queue",
            ErrorSeverity.Playback,
            true);
        }

        private async Task<int> GetCurrentProfileId()
        {
            var profile = await _profileManager.GetCurrentProfileAsync();
            return profile.ProfileID;
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
            _errorHandlingService.SafeExecute(() =>
            {
                if (track == null || allTracks == null || !allTracks.Any())
                {
                    throw new ArgumentException("Track or tracks collection is null or empty");
                }

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
                    throw new InvalidOperationException("Selected track not found in the provided track collection");
                }

                // Update state atomically
                NowPlayingQueue = newQueue;
                _currentTrackIndex = targetIndex;
                SetCurrentTrack(_currentTrackIndex);

                // Notify subscribers
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));
                UpdateDurations();
            },
            "Playing track",
            ErrorSeverity.Playback,
            true);
        }

        public void AddToPlayNext(ObservableCollection<TrackDisplayModel> tracksToAdd)
        {
            _errorHandlingService.SafeExecute(() =>
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
            },
            "Adding tracks to play next",
            ErrorSeverity.NonCritical,
            true);
        }

        public void AddTrackToQueue(ObservableCollection<TrackDisplayModel> tracks)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (tracks == null || !tracks.Any()) return;

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
            },
            "Adding tracks to queue",
            ErrorSeverity.NonCritical,
            false);
        }

        public int GetNextTrack()
        {
            return _errorHandlingService.SafeExecute(() =>
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
            },
            "Getting next track",
            -1,
            ErrorSeverity.Playback,
            false);
        }

        public int GetPreviousTrack()
        {
            return _errorHandlingService.SafeExecute(() =>
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
            },
            "Getting previous track",
            -1,
            ErrorSeverity.Playback,
            false);
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
            _errorHandlingService.SafeExecute(() =>
            {
                if (newQueue == null || !newQueue.Any())
                {
                    throw new ArgumentException("New queue is null or empty");
                }

                if (newIndex < 0 || newIndex >= newQueue.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(newIndex), "Track index is out of range");
                }

                NowPlayingQueue = newQueue;
                CurrentTrack = NowPlayingQueue[newIndex];
                _currentTrackIndex = newIndex;
            },
            "Updating queue and current track",
            ErrorSeverity.Playback,
            true);
        }

        public void ToggleShuffle()
        {
            _errorHandlingService.SafeExecute(() =>
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

                        // Ensure current track index is valid
                        if (_currentTrackIndex < 0 || _currentTrackIndex >= NowPlayingQueue.Count)
                        {
                            _currentTrackIndex = 0;
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
                        foreach (var t in shuffledBefore)
                        {
                            NowPlayingQueue.Add(t);
                        }
                        NowPlayingQueue.Add(currentTrack); // Current track stays in same position
                        foreach (var t in shuffledAfter)
                        {
                            NowPlayingQueue.Add(t);
                        }
                    }
                    else
                    {
                        // Ensure we have a valid current track
                        var currentTrack = _currentTrackIndex >= 0 && _currentTrackIndex < NowPlayingQueue.Count
                            ? NowPlayingQueue[_currentTrackIndex]
                            : null;

                        // Restore original order using NowPlayingPosition
                        var orderedTracks = NowPlayingQueue
                            .OrderBy(t => t.NowPlayingPosition)
                            .ToList();

                        NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(orderedTracks);

                        // Update current track index
                        if (currentTrack != null)
                        {
                            _currentTrackIndex = orderedTracks.IndexOf(currentTrack);
                            if (_currentTrackIndex < 0) _currentTrackIndex = 0;
                        }
                    }

                    SaveCurrentQueueState().ConfigureAwait(false);
                }
            },
            "Toggling shuffle mode",
            ErrorSeverity.NonCritical);
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

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                await _queueService.SaveCurrentQueueState(
                    await GetCurrentProfileId(),
                    NowPlayingQueue.ToList(),
                    _currentTrackIndex,
                    IsShuffled,
                    RepeatMode.ToString()
                );
            },
            "Saving queue state",
            ErrorSeverity.NonCritical,
            false);
        }

        public async Task SaveReorderedQueue(List<TrackDisplayModel> reorderedTracks, int newCurrentTrackIndex)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (reorderedTracks == null || !reorderedTracks.Any())
                {
                    throw new ArgumentException("Reordered tracks list is null or empty");
                }

                if (newCurrentTrackIndex < 0 || newCurrentTrackIndex >= reorderedTracks.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(newCurrentTrackIndex), "New track index is out of range");
                }

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
            },
            "Saving reordered queue",
            ErrorSeverity.NonCritical,
            true);
        }

        public void UpdateDurations()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (NowPlayingQueue == null || !NowPlayingQueue.Any())
                {
                    TotalDuration = TimeSpan.Zero;
                    RemainingDuration = TimeSpan.Zero;
                    return;
                }

                // Calculate total duration with null-checking
                TotalDuration = TimeSpan.FromTicks(
                    NowPlayingQueue
                        .Where(t => t != null)
                        .Sum(t => t.Duration.Ticks));

                if (CurrentTrack != null)
                {
                    var currentIndex = NowPlayingQueue.IndexOf(CurrentTrack);
                    if (currentIndex >= 0)
                    {
                        RemainingDuration = TimeSpan.FromTicks(
                            NowPlayingQueue
                                .Skip(currentIndex)
                                .Where(t => t != null)
                                .Sum(t => t.Duration.Ticks));
                    }
                    else
                    {
                        RemainingDuration = TotalDuration;
                    }
                }
                else
                {
                    RemainingDuration = TotalDuration;
                }
            },
            "Updating queue durations",
            ErrorSeverity.NonCritical,
            false);
        }

    }

}
