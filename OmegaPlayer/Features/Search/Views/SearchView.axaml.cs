using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Search.ViewModels;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Search.Views
{
    public partial class SearchView : UserControl
    {
        private ScrollViewer _searchScrollViewer;
        private ItemsControl _tracksItemsControl;
        private ItemsControl _albumsItemsControl;
        private ItemsControl _artistsItemsControl;
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;

        // Timer-based visibility checking for performance
        private DispatcherTimer _visibilityCheckTimer;
        private ScrollViewer _cachedScrollViewer;

        // Track which items are currently visible
        private HashSet<int> _visibleTrackIndexes = new HashSet<int>();
        private HashSet<int> _visibleAlbumIndexes = new HashSet<int>();
        private HashSet<int> _visibleArtistIndexes = new HashSet<int>();

        private SearchViewModel ViewModel => DataContext as SearchViewModel;

        public SearchView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Try to get error handling service first
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

            // Initialize timer for batched visibility checks (performance optimization)
            _visibilityCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Check every 100ms instead of on every scroll event
            };
            _visibilityCheckTimer.Tick += (s, e) =>
            {
                _visibilityCheckTimer.Stop();
                CheckVisibleItems(_cachedScrollViewer);
            };

            // Wait for UI to load then capture references
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Wire up visibility triggers if ViewModel is available
                    if (DataContext is SearchViewModel viewModel)
                    {
                        viewModel.TriggerTracksVisibilityCheck = () =>
                        {
                            Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                        };
                        viewModel.TriggerAlbumsVisibilityCheck = () =>
                        {
                            Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                        };
                        viewModel.TriggerArtistsVisibilityCheck = () =>
                        {
                            Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                        };
                    }

                    // Find the main ScrollViewer
                    _searchScrollViewer = this.FindControl<ScrollViewer>("SearchScrollViewer");
                    if (_searchScrollViewer != null)
                    {
                        _searchScrollViewer.ScrollChanged += OnScrollChanged;
                        _cachedScrollViewer = _searchScrollViewer;
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing UI element",
                            "Could not find SearchScrollViewer element in search view",
                            null,
                            false);
                    }

                    // Find the ItemsControls
                    _tracksItemsControl = this.FindControl<ItemsControl>("TracksItemsControl");
                    _albumsItemsControl = this.FindControl<ItemsControl>("AlbumsItemsControl");
                    _artistsItemsControl = this.FindControl<ItemsControl>("ArtistsItemsControl");

                    if (_tracksItemsControl == null || _albumsItemsControl == null || _artistsItemsControl == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing UI elements",
                            "Could not find one or more ItemsControl elements in search view",
                            null,
                            false);
                    }

                    // Check initial visibility after a short delay to ensure UI is rendered
                    Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                },
                "Loading search view controls",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isDisposed) return;

            try
            {
                _cachedScrollViewer = sender as ScrollViewer;

                // Use timer-based batching to reduce excessive calls during fast scrolling
                _visibilityCheckTimer.Stop();
                _visibilityCheckTimer.Start();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling scroll change in SearchView",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private async void CheckVisibleItems()
        {
            if (_isDisposed) return;

            await CheckVisibleItems(_cachedScrollViewer);
        }

        private async Task CheckVisibleItems(ScrollViewer scrollViewer)
        {
            if (_isDisposed || ViewModel == null) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Ensure we have a ScrollViewer (might be null when initially called)
                    if (scrollViewer == null)
                    {
                        scrollViewer = _cachedScrollViewer ?? this.FindControl<ScrollViewer>("SearchScrollViewer");
                        if (scrollViewer == null) return;
                    }

                    // Check visible tracks
                    if (_tracksItemsControl != null)
                    {
                        await CheckVisibleItemsInControl(
                            _tracksItemsControl,
                            ViewModel.Tracks,
                            _visibleTrackIndexes,
                            scrollViewer,
                            (item, visible) => ViewModel.NotifyTrackVisible(item as TrackDisplayModel, visible));
                    }

                    // Check visible albums
                    if (_albumsItemsControl != null)
                    {
                        await CheckVisibleItemsInControl(
                            _albumsItemsControl,
                            ViewModel.Albums,
                            _visibleAlbumIndexes,
                            scrollViewer,
                            (item, visible) => ViewModel.NotifyAlbumVisible(item as AlbumDisplayModel, visible));
                    }

                    // Check visible artists
                    if (_artistsItemsControl != null)
                    {
                        await CheckVisibleItemsInControl(
                            _artistsItemsControl,
                            ViewModel.Artists,
                            _visibleArtistIndexes,
                            scrollViewer,
                            (item, visible) => ViewModel.NotifyArtistVisible(item as ArtistDisplayModel, visible));
                    }
                },
                "Checking visible items in search view",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task CheckVisibleItemsInControl<T>(
            ItemsControl itemsControl,
            IList<T> items,
            HashSet<int> visibleIndexes,
            ScrollViewer scrollViewer,
            Func<object, bool, Task> notifyCallback)
        {
            if (items == null || items.Count == 0) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var newVisibleIndexes = new HashSet<int>();
                    var containers = itemsControl.GetRealizedContainers();

                    // Cache viewport dimensions for performance
                    var viewportHeight = scrollViewer.Viewport.Height;
                    var buffer = 100; // 100px buffer for preloading

                    foreach (var container in containers)
                    {
                        if (container == null) continue;

                        try
                        {
                            // Get the container's position relative to the scroll viewer
                            var transform = container.TransformToVisual(scrollViewer);
                            if (transform != null)
                            {
                                var containerBounds = container.Bounds;
                                var containerTop = transform.Value.Transform(new Point(0, 0)).Y;
                                var containerHeight = containerBounds.Height;
                                var containerBottom = containerTop + containerHeight;

                                // Check if the container is in the viewport (with some buffer)
                                bool isVisible = (containerBottom > -buffer && containerTop < viewportHeight + buffer);

                                // Get the container's index
                                int index = itemsControl.IndexFromContainer(container);

                                if (isVisible && index >= 0)
                                {
                                    newVisibleIndexes.Add(index);

                                    // If not previously visible, notify it's now visible
                                    if (!visibleIndexes.Contains(index))
                                    {
                                        if (index < items.Count)
                                        {
                                            var item = items[index];
                                            // Don't await this to prevent blocking UI
                                            _ = notifyCallback(item, true);
                                        }
                                    }
                                }
                                else if (visibleIndexes.Contains(index))
                                {
                                    // Was visible before but not anymore
                                    if (index >= 0 && index < items.Count)
                                    {
                                        var item = items[index];
                                        // Don't await this to prevent blocking UI
                                        _ = notifyCallback(item, false);
                                    }
                                }
                            }
                        }
                        catch (Exception itemEx)
                        {
                            // Log error but continue processing other items
                            _errorHandlingService?.LogError(
                                ErrorSeverity.NonCritical,
                                "Error processing container visibility in SearchView",
                                $"Failed to process visibility for an item container: {itemEx.Message}",
                                itemEx,
                                false);
                        }
                    }

                    // Update the visible indexes
                    visibleIndexes.Clear();
                    foreach (var index in newVisibleIndexes)
                    {
                        visibleIndexes.Add(index);
                    }
                },
                $"Checking visible items in {itemsControl.Name}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mark as disposed to prevent further updates
                _isDisposed = true;

                // Stop the timer
                _visibilityCheckTimer?.Stop();

                // Clean up event handlers
                if (_searchScrollViewer != null)
                {
                    _searchScrollViewer.ScrollChanged -= OnScrollChanged;
                }

                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear tracking collections to help GC
                _visibleTrackIndexes.Clear();
                _visibleAlbumIndexes.Clear();
                _visibleArtistIndexes.Clear();

                // Clear references
                _tracksItemsControl = null;
                _albumsItemsControl = null;
                _artistsItemsControl = null;
                _cachedScrollViewer = null;
                _searchScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during SearchView unload",
                    "Failed to properly clean up resources during view unload.",
                    ex,
                    false);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                // Clean up resources when control is detached
                _isDisposed = true;

                // Stop the timer
                _visibilityCheckTimer?.Stop();
                _visibilityCheckTimer = null;

                // Clear tracking collections
                _visibleTrackIndexes.Clear();
                _visibleAlbumIndexes.Clear();
                _visibleArtistIndexes.Clear();

                // If any cleanup was missed in OnUnloaded, handle it here
                if (_searchScrollViewer != null)
                {
                    _searchScrollViewer.ScrollChanged -= OnScrollChanged;
                }

                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear references
                _tracksItemsControl = null;
                _albumsItemsControl = null;
                _artistsItemsControl = null;
                _cachedScrollViewer = null;
                _searchScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error detaching SearchView",
                    "Failed to properly clean up resources when detaching from visual tree.",
                    ex,
                    false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}