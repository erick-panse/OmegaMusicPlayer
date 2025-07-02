using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.UI;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using System;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class AlbumsView : UserControl
    {
        private ItemsControl _albumsItemsControl;
        private HashSet<int> _visibleAlbumIndexes = new HashSet<int>();
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;
        private DispatcherTimer _visibilityCheckTimer;
        private ScrollViewer _cachedScrollViewer;

        public AlbumsView()
        {
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

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

            // Hook into the Loaded event to find the ItemsControl
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Wire up visibility trigger if ViewModel is available
            if (DataContext is AlbumsViewModel viewModel)
            {
                viewModel.TriggerVisibilityCheck = () =>
                {
                    Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                };
            }

            _albumsItemsControl = this.FindControl<ItemsControl>("AlbumsItemsControl");

            // Cache the scroll viewer for performance
            _cachedScrollViewer = this.FindControl<ScrollViewer>("AlbumsScrollViewer");

            // Check initially visible items
            if (_albumsItemsControl != null)
            {
                // Delay slightly to ensure containers are realized
                Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
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
                    "Error handling scroll change in AlbumsView",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private async void CheckVisibleItems(ScrollViewer scrollViewer)
        {
            if (_isDisposed) return;

            try
            {
                if (DataContext is not AlbumsViewModel viewModel || _albumsItemsControl == null)
                    return;

                // Ensure we have a ScrollViewer (might be null when initially called)
                if (scrollViewer == null)
                {
                    scrollViewer = _cachedScrollViewer ?? this.FindControl<ScrollViewer>("AlbumsScrollViewer");
                    if (scrollViewer == null) return;
                }

                // Keep track of which items are currently visible
                var newVisibleIndexes = new HashSet<int>();

                // Get all item containers that are currently realized
                var containers = _albumsItemsControl.GetRealizedContainers();

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
                            int index = _albumsItemsControl.IndexFromContainer(container);

                            if (isVisible && index >= 0)
                            {
                                newVisibleIndexes.Add(index);

                                // If not previously visible, notify it's now visible
                                if (!_visibleAlbumIndexes.Contains(index))
                                {
                                    if (index < viewModel.Albums.Count)
                                    {
                                        var album = viewModel.Albums[index];
                                        // Don't await this to prevent blocking UI
                                        _ = viewModel.NotifyAlbumVisible(album, true);
                                    }
                                }
                            }
                            else if (_visibleAlbumIndexes.Contains(index))
                            {
                                // Was visible before but not anymore
                                if (index >= 0 && index < viewModel.Albums.Count)
                                {
                                    var album = viewModel.Albums[index];
                                    // Don't await this to prevent blocking UI
                                    _ = viewModel.NotifyAlbumVisible(album, false);
                                }
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        // Log error but continue processing other items
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error processing container visibility on AlbumsView",
                            $"Failed to process visibility for an item container: {itemEx.Message}",
                            itemEx,
                            false);
                    }
                }
                // Update the visible indexes
                _visibleAlbumIndexes = newVisibleIndexes;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating visible albums",
                    "Failed to update visibility tracking for image loading optimization.",
                    ex,
                    false);
            }
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
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear tracking collections to help GC
                _visibleAlbumIndexes.Clear();
                _albumsItemsControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during AlbumsView unload",
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

                _visibleAlbumIndexes.Clear();

                // If any cleanup was missed in OnUnloaded, handle it here
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear references
                _albumsItemsControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error detaching AlbumsView",
                    "Failed to properly clean up resources when detaching from visual tree.",
                    ex,
                    false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}