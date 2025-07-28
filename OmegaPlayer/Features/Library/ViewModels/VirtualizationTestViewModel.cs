using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.ViewModels
{
    /// <summary>
    /// Test ViewModel to evaluate ItemsControl virtualization performance
    /// </summary>
    public partial class VirtualizationTestViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _testTracks = new();

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _performanceStats;

        [ObservableProperty]
        private int _trackCount = 2000;

        [ObservableProperty]
        private long _memoryBeforeLoad;

        [ObservableProperty]
        private long _memoryAfterLoad;

        [ObservableProperty]
        private long _loadTimeMs;

        private readonly IMessenger _messenger;
        private Stopwatch _stopwatch = new();

        public VirtualizationTestViewModel(IMessenger messenger)
        {
            _messenger = messenger;
        }

        [RelayCommand]
        public async Task GenerateTestTracks()
        {
            IsLoading = true;
            TestTracks.Clear();

            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            MemoryBeforeLoad = GC.GetTotalMemory(false) / 1024 / 1024; // MB

            _stopwatch.Restart();

            var tracks = await Task.Run(() =>
            {
                var tracks = new List<TrackDisplayModel>();

                for (int i = 0; i < TrackCount; i++)
                {
                    var track = new TrackDisplayModel()
                    {
                        TrackID = i,
                        Title = $"Test Track {i:D4}",
                        AlbumID = i / 10,
                        AlbumTitle = $"Test Album {i / 10:D3}",
                        Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(i % 60)),
                        FilePath = $"/fake/path/track_{i}.mp3",
                        Genre = $"Genre {i % 20}",
                        Artists = new List<Artists>
                        {
                            new Artists { ArtistID = i % 100, ArtistName = $"Artist {i % 100:D3}" }
                        },
                        ReleaseDate = DateTime.Now.AddDays(-i),
                        PlayCount = i % 50,
                        BitRate = 320,
                        FileType = "MP3",
                        Position = i,
                        CoverPath = null, // No images for performance test
                        IsLiked = i % 7 == 0
                    };

                    tracks.Add(track);
                }

                return tracks;
            });

            // Add tracks to UI collection in batches to measure UI creation time
            var batchSize = 100;
            for (int i = 0; i < TrackCount; i += batchSize)
            {
                var batch = tracks.Skip(i).Take(batchSize).ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var track in batch)
                    {
                        TestTracks.Add(track);
                    }
                });

                // Small delay to allow UI to process
                await Task.Delay(1);
            }

            _stopwatch.Stop();
            LoadTimeMs = _stopwatch.ElapsedMilliseconds;

            // Force another GC to measure final memory
            await Task.Delay(100); // Let UI settle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            MemoryAfterLoad = GC.GetTotalMemory(false) / 1024 / 1024; // MB

            UpdatePerformanceStats();
            IsLoading = false;
        }

        [RelayCommand]
        public void ChangeViewType(string viewType)
        {
            var oldViewType = CurrentViewType;

            _stopwatch.Restart();

            CurrentViewType = viewType.ToLower() switch
            {
                "list" => ViewType.List,
                "card" => ViewType.Card,
                "image" => ViewType.Image,
                "roundimage" => ViewType.RoundImage,
                _ => ViewType.Card
            };

            // Measure view switch time after a brief delay for UI update
            Task.Delay(100).ContinueWith(_ =>
            {
                _stopwatch.Stop();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    PerformanceStats += $"\nView switch ({oldViewType} -> {CurrentViewType}): {_stopwatch.ElapsedMilliseconds}ms";
                });
            });
        }

        [RelayCommand]
        public void ClearTracks()
        {
            _stopwatch.Restart();

            TestTracks.Clear();

            // Force GC and measure cleanup time
            Task.Run(async () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await Task.Delay(100);
                _stopwatch.Stop();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var memoryAfterClear = GC.GetTotalMemory(false) / 1024 / 1024;
                    PerformanceStats += $"\nCleanup time: {_stopwatch.ElapsedMilliseconds}ms";
                    PerformanceStats += $"\nMemory after clear: {memoryAfterClear}MB";
                });
            });
        }

        [RelayCommand]
        public async Task SimulateScrolling()
        {
            // This would be called from the view during scroll testing
            PerformanceStats += $"\nScroll test started at {DateTime.Now:HH:mm:ss}";

            // Measure scroll responsiveness by tracking UI thread availability
            var scrollStartTime = DateTime.Now;

            for (int i = 0; i < 10; i++)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                    // Simulate scroll update work
                }, Avalonia.Threading.DispatcherPriority.Background);

                await Task.Delay(16); // ~60fps
            }

            var scrollEndTime = DateTime.Now;
            var scrollDuration = (scrollEndTime - scrollStartTime).TotalMilliseconds;

            PerformanceStats += $"\nScroll simulation (10 frames): {scrollDuration:F0}ms";
        }

        private void UpdatePerformanceStats()
        {
            PerformanceStats = $"Performance Test Results:\n" +
                              $"Tracks Generated: {TrackCount:N0}\n" +
                              $"Load Time: {LoadTimeMs:N0}ms\n" +
                              $"Memory Before: {MemoryBeforeLoad}MB\n" +
                              $"Memory After: {MemoryAfterLoad}MB\n" +
                              $"Memory Used: {MemoryAfterLoad - MemoryBeforeLoad}MB\n" +
                              $"Avg per Track: {(double)(MemoryAfterLoad - MemoryBeforeLoad) / TrackCount * 1024:F1}KB\n" +
                              $"Current View: {CurrentViewType}";
        }

        // Helper method to measure UI responsiveness
        public async Task<long> MeasureUIResponsiveness()
        {
            var sw = Stopwatch.StartNew();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Simple UI operation
            }, Avalonia.Threading.DispatcherPriority.Normal);

            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
    }
}