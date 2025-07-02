using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class FoldersView : UserControl
    {
        private ItemsControl _foldersItemsControl;
        private HashSet<int> _visibleFolderIndexes = new HashSet<int>();
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;
        private DispatcherTimer _visibilityCheckTimer;
        private ScrollViewer _cachedScrollViewer;

        public FoldersView()
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
            if (DataContext is FoldersViewModel viewModel)
            {
                viewModel.TriggerVisibilityCheck = () =>
                {
                    Dispatcher.UIThread.Post(() => CheckVisibleItems(_cachedScrollViewer), DispatcherPriority.Background);
                };
            }

            _foldersItemsControl = this.FindControl<ItemsControl>("FoldersItemsControl");

            // Cache the scroll viewer for performance
            _cachedScrollViewer = this.FindControl<ScrollViewer>("FoldersScrollViewer");

            // Check initially visible items
            if (_foldersItemsControl != null)
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
                    "Error handling scroll change in FoldersView",
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
                if (DataContext is not FoldersViewModel viewModel || _foldersItemsControl == null)
                    return;

                // Ensure we have a ScrollViewer (might be null when initially called)
                if (scrollViewer == null)
                {
                    scrollViewer = _cachedScrollViewer ?? this.FindControl<ScrollViewer>("FoldersScrollViewer");
                    if (scrollViewer == null) return;
                }

                // Keep track of which items are currently visible
                var newVisibleIndexes = new HashSet<int>();

                // Get all item containers that are currently realized
                var containers = _foldersItemsControl.GetRealizedContainers();

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
                            int index = _foldersItemsControl.IndexFromContainer(container);

                            if (isVisible && index >= 0)
                            {
                                newVisibleIndexes.Add(index);

                                // If not previously visible, notify it's now visible
                                if (!_visibleFolderIndexes.Contains(index))
                                {
                                    if (index < viewModel.Folders.Count)
                                    {
                                        var folder = viewModel.Folders[index];
                                        // Don't await this to prevent blocking UI
                                        _ = viewModel.NotifyFolderVisible(folder, true);
                                    }
                                }
                            }
                            else if (_visibleFolderIndexes.Contains(index))
                            {
                                // Was visible before but not anymore
                                if (index >= 0 && index < viewModel.Folders.Count)
                                {
                                    var folder = viewModel.Folders[index];
                                    // Don't await this to prevent blocking UI
                                    _ = viewModel.NotifyFolderVisible(folder, false);
                                }
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        // Log error but continue processing other items
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error processing container visibility on FoldersView",
                            $"Failed to process visibility for an item container: {itemEx.Message}",
                            itemEx,
                            false);
                    }
                }
                // Update the visible indexes
                _visibleFolderIndexes = newVisibleIndexes;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating visible folders",
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
                _visibleFolderIndexes.Clear();
                _foldersItemsControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during FoldersView unload",
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

                _visibleFolderIndexes.Clear();

                // If any cleanup was missed in OnUnloaded, handle it here
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear references
                _foldersItemsControl = null;
                _cachedScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error detaching FoldersView",
                    "Failed to properly clean up resources when detaching from visual tree.",
                    ex,
                    false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}