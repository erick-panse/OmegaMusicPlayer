using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.LibraryEnums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Core.ViewModels;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Playback.Models;
using OmegaMusicPlayer.Features.Playback.Services;
using OmegaMusicPlayer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Playback.ViewModels
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

    public partial class TrackQueueViewModel : ViewModelBase
    {
        private readonly QueueService _queueService;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly ProfileManager _profileManager;
        private readonly PlayHistoryService _playHistoryService;
        private readonly TrackStatsService _trackStatsService;
        private readonly QueueSaveCoordinator _queueSaveCoordinator;
        private readonly LocalizationService _localizationService;
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
            QueueSaveCoordinator queueSaveCoordinator,
            LocalizationService localizationService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _queueService = queueService;
            _trackDisplayService = trackDisplayService;
            _profileManager = profileManager;
            _playHistoryService = playHistoryService;
            _trackStatsService = trackStatsService;
            _queueSaveCoordinator = queueSaveCoordinator;
            _localizationService = localizationService;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            _ = LoadLastPlayedQueue();
        }

        partial void OnCurrentTrackChanged(TrackDisplayModel value)
        {
            // Send message when current track changes
            _messenger.Send(new CurrentTrackChangedMessage(value));
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

                    await ClearQueue();
                    return;
                }

                try
                {
                    // Set queue state
                    IsShuffled = queueState.IsShuffled;
                    RepeatMode = Enum.TryParse<RepeatMode>(queueState.RepeatMode, true, out var repeatMode)
                        ? repeatMode
                        : RepeatMode.None;

                    // Get tracks in their original order always
                    var orderedTracks = queueState.Tracks.OrderBy(t => t.OriginalOrder).ToList();

                    // Get track display models
                    var originalTracks = await _trackDisplayService.GetTrackDisplaysFromQueue(orderedTracks);
                    if (originalTracks == null || !originalTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to load track display data",
                            "Could not retrieve track display information for queue tracks.",
                            null,
                            false);
                        return;
                    }

                    // Always store the original (unshuffled) queue
                    _originalQueue = new ObservableCollection<TrackDisplayModel>(originalTracks);

                    if (IsShuffled)
                    {
                        // If shuffled, get tracks in their current order
                        var shuffledTracks = queueState.Tracks.OrderBy(t => t.TrackOrder).ToList();
                        var displayTracks = await _trackDisplayService.GetTrackDisplaysFromQueue(shuffledTracks);

                        if (displayTracks == null || !displayTracks.Any())
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to load shuffled track display data",
                                "Could not retrieve track display information for queue tracks.",
                                null,
                                false);
                            return;
                        }

                        // Use the shuffled tracks for the playing queue
                        NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(displayTracks);
                    }
                    else
                    {
                        // If not shuffled, use original tracks for the playing queue
                        NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(originalTracks);
                    }

                    // Ensure current track index is valid
                    int currentTrackIndex = queueState.CurrentQueue.CurrentTrackOrder;
                    if (currentTrackIndex < 0 || currentTrackIndex >= NowPlayingQueue.Count)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid current track index",
                            $"Current track index ({currentTrackIndex}) is out of range for queue with {NowPlayingQueue.Count} tracks. Resetting to 0.",
                            null,
                            false);
                        currentTrackIndex = 0;
                    }

                    // Set current track
                    var currentTrack = NowPlayingQueue.ElementAtOrDefault(currentTrackIndex);
                    if (currentTrack != null)
                    {
                        CurrentTrack = currentTrack;
                        _currentTrackIndex = currentTrackIndex;
                    }
                    else if (NowPlayingQueue.Any())
                    {
                        // Fallback to first track
                        CurrentTrack = NowPlayingQueue.First();
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
            _localizationService["ErrorLoadingLastPlayedQueue"],
            ErrorSeverity.Playback,
            true);
        }

        private async Task<int> GetCurrentProfileId()
        {
            var profile = await _profileManager.GetCurrentProfileAsync();
            return profile.ProfileID;
        }

        private async Task SetCurrentTrack(int trackIndex)
        {
            CurrentTrack = trackIndex >= 0 && trackIndex < NowPlayingQueue.Count
                ? NowPlayingQueue[trackIndex]
                : null;

            await SaveCurrentTrack();
            UpdateDurations();
        }
        public int GetCurrentTrackIndex()
        {
            return _currentTrackIndex;
        }

        public async Task PlayThisTrack(TrackDisplayModel track, ObservableCollection<TrackDisplayModel> allTracks, bool shuffleQueue = false)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (track == null || allTracks == null || !allTracks.Any())
                {
                    throw new ArgumentException("Track or tracks collection is null or empty");
                }

                if (IsShuffled)
                {
                    IsShuffled = false; // Reset IsShuffled
                }

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

                if (shuffleQueue)
                {
                    ToggleShuffle(true);
                }

                await SetCurrentTrack(_currentTrackIndex);

                // Notify subscribers
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));
                UpdateDurations();

                // Use coordinator for full queue save
                await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                {
                    int profileId = await GetCurrentProfileId();
                    await _queueService.SaveCurrentQueueState(
                        profileId,
                        NowPlayingQueue.ToList(),
                        _currentTrackIndex,
                        IsShuffled,
                        RepeatMode.ToString(),
                        null,
                        ct);
                });
            },
            _localizationService["ErrorPlayingTrack"],
            ErrorSeverity.Playback,
            true);
        }

        public void AddToPlayNext(ObservableCollection<TrackDisplayModel> tracksToAdd)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (tracksToAdd == null || !tracksToAdd.Any())
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Could not find Songs to add to play next",
                        "",
                        null,
                        true);
                    return;
                }

                // If queue is empty or no track is playing, start fresh
                if (!NowPlayingQueue.Any() || CurrentTrack == null)
                {
                    // Initialize both queues with the same tracks
                    NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(tracksToAdd);
                    _originalQueue = new ObservableCollection<TrackDisplayModel>(tracksToAdd);

                    _currentTrackIndex = 0;
                    CurrentTrack = NowPlayingQueue[_currentTrackIndex];

                    _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));

                    // Use coordinator for full queue save
                    Task.Run(async () =>
                    {
                        await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                        {
                            int profileId = await GetCurrentProfileId();
                            await _queueService.SaveCurrentQueueState(
                                profileId,
                                NowPlayingQueue.ToList(),
                                _currentTrackIndex,
                                IsShuffled,
                                RepeatMode.ToString(),
                                null,
                                ct);
                        });
                    });

                    UpdateDurations();
                    return;
                }

                // Insert after current track in playing queue
                var insertIndex = _currentTrackIndex + 1;
                foreach (var track in tracksToAdd.Reverse()) // Reverse to maintain order when inserting
                {
                    NowPlayingQueue.Insert(insertIndex, track);
                }

                // If shuffled, also insert in original queue
                if (IsShuffled && _originalQueue != null)
                {
                    foreach (var track in tracksToAdd.Reverse())
                    {
                        _originalQueue.Insert(insertIndex, track);
                    }
                }

                // Use coordinator for full queue save
                Task.Run(async () =>
                {
                    await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                    {
                        int profileId = await GetCurrentProfileId();
                        await _queueService.SaveCurrentQueueState(
                            profileId,
                            NowPlayingQueue.ToList(),
                            _currentTrackIndex,
                            IsShuffled,
                            RepeatMode.ToString(),
                            null,
                            ct);
                    });
                });

                UpdateDurations();
            },
            _localizationService["ErrorAddingPlayNext"],
            ErrorSeverity.NonCritical,
            true);
        }

        public void AddTrackToQueue(ObservableCollection<TrackDisplayModel> tracksToAdd)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (tracksToAdd == null || !tracksToAdd.Any())
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Could not find Songs to add in Queue",
                        "",
                        null,
                        true);
                    return;
                }

                // If queue is empty or no track is playing, start fresh
                if (!NowPlayingQueue.Any() || CurrentTrack == null)
                {
                    // Initialize both queues with the same tracks
                    NowPlayingQueue = new ObservableCollection<TrackDisplayModel>(tracksToAdd);
                    _originalQueue = new ObservableCollection<TrackDisplayModel>(tracksToAdd);

                    _currentTrackIndex = 0;
                    CurrentTrack = NowPlayingQueue[_currentTrackIndex];

                    _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex));

                    // Use coordinator for full queue save
                    Task.Run(async () =>
                    {
                        await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                        {
                            int profileId = await GetCurrentProfileId();
                            await _queueService.SaveCurrentQueueState(
                                profileId,
                                NowPlayingQueue.ToList(),
                                _currentTrackIndex,
                                IsShuffled,
                                RepeatMode.ToString(),
                                null,
                                ct);
                        });
                    });

                    UpdateDurations();
                    return;
                }

                // Add tracks to the end of playing queue
                foreach (var track in tracksToAdd)
                {
                    NowPlayingQueue.Add(track);
                }

                // Also add to original queue if shuffled
                if (IsShuffled && _originalQueue != null)
                {
                    foreach (var track in tracksToAdd)
                    {
                        _originalQueue.Add(track);
                    }
                }

                // Use coordinator for full queue save
                Task.Run(async () =>
                {
                    await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                    {
                        int profileId = await GetCurrentProfileId();
                        await _queueService.SaveCurrentQueueState(
                            profileId,
                            NowPlayingQueue.ToList(),
                            _currentTrackIndex,
                            IsShuffled,
                            RepeatMode.ToString(),
                            null,
                            ct);
                    });
                });

                UpdateDurations();
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
            if (newIndex < 0) return;

            _currentTrackIndex = newIndex;
            CurrentTrack = NowPlayingQueue[_currentTrackIndex];
            UpdateDurations();

            // Use debounced metadata save for navigation
            _queueSaveCoordinator.ScheduleMetadataSave(async (ct) =>
            {
                int profileId = await GetCurrentProfileId();
                await _queueService.SaveQueueMetadataOnly(
                    profileId,
                    _currentTrackIndex,
                    IsShuffled,
                    RepeatMode.ToString(),
                    ct);
            });

            // Save play statistics immediately (separate from queue state)
            await IncrementPlayCount();
            await _playHistoryService.AddToHistory(CurrentTrack);
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
            false);
        }

        public void ToggleShuffle(bool playFromScratch = false)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (!NowPlayingQueue.Any()) return;

                IsShuffled = !IsShuffled;

                if (IsShuffled)
                {
                    // Turning shuffle ON
                    _originalQueue = new ObservableCollection<TrackDisplayModel>();
                    foreach (var track in NowPlayingQueue)
                    {
                        _originalQueue.Add(track);
                    }

                    // Shuffle entire list
                    var shuffledBefore = NowPlayingQueue.OrderBy(x => Guid.NewGuid()).ToList();

                    // Reconstruct queue maintaining current track position
                    NowPlayingQueue.Clear();
                    foreach (var t in shuffledBefore)
                    {
                        NowPlayingQueue.Add(t);
                    }

                    _currentTrackIndex = NowPlayingQueue.IndexOf(CurrentTrack);

                    if (_currentTrackIndex < 0 || _currentTrackIndex >= NowPlayingQueue.Count)
                    {
                        _currentTrackIndex = 0;
                    }

                    if (playFromScratch)
                    {
                        _currentTrackIndex = 0;
                        CurrentTrack = NowPlayingQueue[_currentTrackIndex];
                    }
                }
                else
                {
                    // Turning shuffle OFF
                    if (_originalQueue == null || !_originalQueue.Any())
                    {
                        return;
                    }

                    // Restore the queue to original order
                    NowPlayingQueue.Clear();
                    foreach (var track in _originalQueue)
                    {
                        NowPlayingQueue.Add(track);
                    }

                    _currentTrackIndex = NowPlayingQueue.IndexOf(CurrentTrack);

                    if (_currentTrackIndex < 0 || _currentTrackIndex >= NowPlayingQueue.Count)
                    {
                        _currentTrackIndex = 0;
                    }
                }

                // Notify UI with isShuffleOperation = true to prevent track restart
                _messenger.Send(new TrackQueueUpdateMessage(
                    CurrentTrack,
                    NowPlayingQueue,
                    _currentTrackIndex,
                    isShuffleOperation: true));

                // Use coordinator for full queue save (shuffle changes queue composition)
                Task.Run(async () =>
                {
                    await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                    {
                        int profileId = await GetCurrentProfileId();

                        if (IsShuffled)
                        {
                            // Create shuffled queue tracks
                            var shuffledQueueTracks = new List<QueueTracks>();
                            for (int i = 0; i < NowPlayingQueue.Count; i++)
                            {
                                var track = NowPlayingQueue[i];
                                int originalIndex = FindTrackInQueue(_originalQueue, track);

                                shuffledQueueTracks.Add(new QueueTracks
                                {
                                    TrackID = track.TrackID,
                                    TrackOrder = i,
                                    OriginalOrder = originalIndex
                                });
                            }

                            await _queueService.SaveCurrentQueueState(
                                profileId,
                                NowPlayingQueue.ToList(),
                                _currentTrackIndex,
                                IsShuffled,
                                RepeatMode.ToString(),
                                shuffledQueueTracks,
                                ct);
                        }
                        else
                        {
                            await _queueService.SaveCurrentQueueState(
                                profileId,
                                NowPlayingQueue.ToList(),
                                _currentTrackIndex,
                                IsShuffled,
                                RepeatMode.ToString(),
                                null,
                                ct);
                        }
                    });
                });
            },
            _localizationService["ErrorTogglingShuffleMode"],
            ErrorSeverity.NonCritical,
            true);
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

            // Use coordinator for immediate repeat mode save
            Task.Run(async () =>
            {
                await _queueSaveCoordinator.SaveRepeatModeImmediate(async (ct) =>
                {
                    int profileId = await GetCurrentProfileId();
                    await _queueService.SaveQueueMetadataOnly(
                        profileId,
                        _currentTrackIndex,
                        IsShuffled,
                        RepeatMode.ToString(),
                        ct);
                });
            });
        }

        public async Task IncrementPlayCount()
        {
            if (CurrentTrack != null)
            {
                CurrentTrack.PlayCount++;
                await _trackStatsService.IncrementPlayCount(CurrentTrack.TrackID, CurrentTrack.PlayCount);
            }
        }

        // Method to save only the current track
        public async Task SaveCurrentTrack()
        {
            if (CurrentTrack != null)
            {
                await IncrementPlayCount();

                await _playHistoryService.AddToHistory(CurrentTrack);
            }
        }

        /// <summary>
        /// Helper method to find a track in a queue by matching properties
        /// </summary>
        private int FindTrackInQueue(ObservableCollection<TrackDisplayModel> queue, TrackDisplayModel trackToFind)
        {
            // First try to find the exact same track instance by InstanceId
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].InstanceId == trackToFind.InstanceId)
                {
                    return i;
                }
            }

            // If not found by InstanceId (happens in some edge cases), 
            // fall back to traditional matching by TrackID and position
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].TrackID == trackToFind.TrackID &&
                    queue[i].NowPlayingPosition == trackToFind.NowPlayingPosition)
                {
                    return i;
                }
            }

            // If still not found, try just by TrackID and choose the first occurrence
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].TrackID == trackToFind.TrackID)
                {
                    return i;
                }
            }

            return 0; // Default to first track if not found
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

                // Use coordinator for full queue save
                await _queueSaveCoordinator.SaveFullQueueImmediate(async (ct) =>
                {
                    int profileId = await GetCurrentProfileId();
                    await _queueService.SaveCurrentQueueState(
                        profileId,
                        reorderedTracks,
                        _currentTrackIndex,
                        IsShuffled,
                        RepeatMode.ToString(),
                        null,
                        ct);
                });

                // Notify any subscribers with isShuffleOperation = true to prevent track restart
                _messenger.Send(new TrackQueueUpdateMessage(CurrentTrack, NowPlayingQueue, _currentTrackIndex, isShuffleOperation: true));
            },
            _localizationService["ErrorSavingReorderedQueue"],
            ErrorSeverity.NonCritical,
            true);
        }

        public async Task ClearQueue()
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                _currentTrackIndex = 0;
                CurrentQueueId = 0;
                CurrentTrack = null;
                NowPlayingQueue.Clear();
                await _queueService.ClearCurrentQueueForProfile(await GetCurrentProfileId());

                UpdateDurations();
            },
            "Could not clear queue",
            ErrorSeverity.NonCritical,
            false);
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

        public async Task OnShutdown()
        {
            // Attempt to flush any pending debounced saves
            await _queueSaveCoordinator.FlushPendingSavesOnShutdown();
            _queueSaveCoordinator.Dispose();
        }
    }
}
