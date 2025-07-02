using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Controls.TrackDisplay;
using Avalonia.VisualTree;
using System.Linq;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class DetailsView : UserControl
    {
        private ItemsControl _tracksItemsControl;
        private TrackDisplayControl _trackDisplayControl;
        private HashSet<int> _visibleTrackIndexes = new HashSet<int>();
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;
        private DispatcherTimer _visibilityCheckTimer;
        private ScrollViewer _cachedScrollViewer;

        public DetailsView()
        {
            // Get error handling service from DI container
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
            if (DataContext is DetailsViewModel viewModel)
            {
                viewModel.TriggerVisibilityCheck = () =>
                {
                    Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                };
            }

            // Delay initialization to allow UI to stabilize
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    InitializeControls();
                }
                catch (Exception ex)
                {
                    _errorHandlingService?.LogError(
                        ErrorSeverity.NonCritical,
                        "Error during DetailsView initialization",
                        ex.Message,
                        ex,
                        false);
                }
            }, DispatcherPriority.Background);
        }

        private void InitializeControls()
        {
            // Find the TrackDisplayControl first
            _trackDisplayControl = this.FindControl<TrackDisplayControl>("TrackControl");

            if (_trackDisplayControl != null)
            {
                // Wait for the template to be applied
                if (_trackDisplayControl.IsLoaded)
                {
                    FindTracksItemsControl();
                }
                else
                {
                    // Subscribe to template applied event if not loaded yet
                    _trackDisplayControl.Loaded += TrackDisplayControl_Loaded;
                }
            }
            else
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "TrackDisplayControl not found",
                    "Could not find TrackDisplayControl in DetailsView",
                    null,
                    false);
            }
        }

        private void TrackDisplayControl_Loaded(object sender, RoutedEventArgs e)
        {
            _trackDisplayControl.Loaded -= TrackDisplayControl_Loaded;

            // Delay finding the control to ensure template is fully applied
            Dispatcher.UIThread.Post(() =>
            {
                FindTracksItemsControl();
            }, DispatcherPriority.Background);
        }

        private void FindTracksItemsControl()
        {
            try
            {
                // Try to find the ItemsControl inside the TrackDisplayControl's template
                // We need to traverse the visual tree to find it
                _tracksItemsControl = FindItemsControlInTemplate(_trackDisplayControl);

                if (_tracksItemsControl != null)
                {
                    // Cache the scroll viewer for performance
                    _cachedScrollViewer = this.FindControl<ScrollViewer>("DetailsScrollViewer");

                    // Check initially visible items with a delay to allow UI to settle
                    Dispatcher.UIThread.Post(() =>
                    {
                        CheckVisibleItems(_cachedScrollViewer);
                    }, DispatcherPriority.Background);
                }
                else
                {
                    _errorHandlingService?.LogError(
                        ErrorSeverity.NonCritical,
                        "TracksItemsControl not found in template",
                        "Could not find TracksItemsControl inside TrackDisplayControl template",
                        null,
                        false);
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error finding TracksItemsControl",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private ItemsControl FindItemsControlInTemplate(Visual parent)
        {
            if (parent == null) return null;

            try
            {
                // Check if the current element is an ItemsControl with the right name
                if (parent is ItemsControl itemsControl &&
                    (parent as StyledElement)?.Name == "TracksItemsControl")
                {
                    return itemsControl;
                }

                // Use ToList() to avoid collection modification during enumeration
                var children = parent.GetVisualChildren().ToList();

                // Recursively search through visual children
                foreach (var child in children)
                {
                    var result = FindItemsControlInTemplate(child);
                    if (result != null)
                        return result;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error traversing visual tree",
                    ex.Message,
                    ex,
                    false);
            }

            return null;
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
                    "Error handling scroll change in DetailsView",
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
                if (DataContext is not DetailsViewModel viewModel || _tracksItemsControl == null)
                    return;

                // Ensure we have a ScrollViewer (might be null when initially called)
                if (scrollViewer == null)
                {
                    scrollViewer = _cachedScrollViewer ?? this.FindControl<ScrollViewer>("DetailsScrollViewer");
                    if (scrollViewer == null) return;
                }

                // Keep track of which items are currently visible
                var newVisibleIndexes = new HashSet<int>();

                // Get all item containers that are currently realized
                var containers = _tracksItemsControl.GetRealizedContainers();

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
                            int index = _tracksItemsControl.IndexFromContainer(container);

                            if (isVisible && index >= 0)
                            {
                                newVisibleIndexes.Add(index);

                                // If not previously visible, notify it's now visible
                                if (!_visibleTrackIndexes.Contains(index))
                                {
                                    if (index < viewModel.Tracks.Count)
                                    {
                                        var track = viewModel.Tracks[index];
                                        // Don't await this to prevent blocking UI
                                        _ = viewModel.NotifyTrackVisible(track, true);
                                    }
                                }
                            }
                            else if (_visibleTrackIndexes.Contains(index))
                            {
                                // Was visible before but not anymore
                                if (index >= 0 && index < viewModel.Tracks.Count)
                                {
                                    var track = viewModel.Tracks[index];
                                    // Don't await this to prevent blocking UI
                                    _ = viewModel.NotifyTrackVisible(track, false);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue processing other containers
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error processing container visibility in DetailsView",
                            ex.Message,
                            ex,
                            false);
                    }
                }

                // Update the visible indexes
                _visibleTrackIndexes = newVisibleIndexes;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating visible tracks on DetailsView",
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

                if (_trackDisplayControl != null)
                {
                    _trackDisplayControl.Loaded -= TrackDisplayControl_Loaded;
                }

                // Clear tracking collections to help GC
                _visibleTrackIndexes.Clear();
                _tracksItemsControl = null;
                _trackDisplayControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during DetailsView unload",
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

                _visibleTrackIndexes.Clear();

                // If any cleanup was missed in OnUnloaded, handle it here
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                if (_trackDisplayControl != null)
                {
                    _trackDisplayControl.Loaded -= TrackDisplayControl_Loaded;
                }

                // Clear references
                _tracksItemsControl = null;
                _trackDisplayControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error detaching DetailsView",
                    "Failed to properly clean up resources when detaching from visual tree.",
                    ex,
                    false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}