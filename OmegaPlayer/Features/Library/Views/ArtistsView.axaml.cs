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
    public partial class ArtistsView : UserControl
    {
        private ItemsControl _artistsItemsControl;
        private HashSet<int> _visibleArtistIndexes = new HashSet<int>();
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;

        public ArtistsView()
        {
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Hook into the Loaded event to find the ItemsControl
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _artistsItemsControl = this.FindControl<ItemsControl>("ArtistsItemsControl");

            // Check initially visible items
            if (_artistsItemsControl != null)
            {
                // Delay slightly to ensure containers are realized
                Dispatcher.UIThread.Post(() => CheckVisibleItems(null));
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isDisposed) return;

            if (sender == null) return;
            var scrollViewer = sender as ScrollViewer;

            // If the user scrolls near the end, trigger the load more command
            if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
            {
                // Get the current view's ViewModel
                if (DataContext is ArtistsViewModel artistsViewModel &&
                    artistsViewModel is ILoadMoreItems loadMoreItems &&
                    loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                {
                    loadMoreItems.LoadMoreItemsCommand.Execute(null);
                }
            }

            // Also check for visibility changes
            CheckVisibleItems(scrollViewer);

        }

        private async void CheckVisibleItems(ScrollViewer scrollViewer)
        {
            if (_isDisposed) return;

            try
            {
                if (DataContext is not ArtistsViewModel viewModel || _artistsItemsControl == null)
                    return;

                // Ensure we have a ScrollViewer (might be null when initially called)
                if (scrollViewer == null)
                {
                    scrollViewer = this.FindControl<ScrollViewer>("ArtistsScrollViewer");
                    if (scrollViewer == null) return;
                }

                // Keep track of which items are currently visible
                var newVisibleIndexes = new HashSet<int>();

                // Get all item containers
                var containers = _artistsItemsControl.GetRealizedContainers();
                if (containers == null) return;

                foreach (var container in containers)
                {
                    try
                    {
                        // Get the container's position relative to the scroll viewer
                        var transform = container.TransformToVisual(scrollViewer);
                        if (transform != null)
                        {
                            var containerTop = transform.Value.Transform(new Point(0, 0)).Y;
                            var containerHeight = container.Bounds.Height;
                            var containerBottom = containerTop + containerHeight;

                            // Check if the container is in the viewport (fully or partially)
                            bool isVisible = (containerBottom > 0 && containerTop < scrollViewer.Viewport.Height);

                            // Get the container's index
                            int index = _artistsItemsControl.IndexFromContainer(container);

                            if (isVisible)
                            {
                                newVisibleIndexes.Add(index);

                                // If not previously visible, notify it's now visible
                                if (!_visibleArtistIndexes.Contains(index))
                                {
                                    // Get the artist from the ViewModel
                                    if (index >= 0 && index < viewModel.Artists.Count)
                                    {
                                        var artist = viewModel.Artists[index];
                                        await viewModel.NotifyArtistVisible(artist, true);
                                    }
                                }
                            }
                            else if (_visibleArtistIndexes.Contains(index))
                            {
                                // Was visible before but not anymore
                                if (index >= 0 && index < viewModel.Artists.Count)
                                {
                                    var artist = viewModel.Artists[index];
                                    await viewModel.NotifyArtistVisible(artist, false);
                                }
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        // Log error but continue processing other items
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error processing container visibility",
                            $"Failed to process visibility for an item container: {itemEx.Message}",
                            itemEx,
                            false);
                    }
                }

                // Update the visible indexes
                _visibleArtistIndexes = newVisibleIndexes;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating visible artists on ArtistsView",
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

                // Clean up event handlers
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                // Clear tracking collections to help GC
                _visibleArtistIndexes.Clear();
                _artistsItemsControl = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during ArtistsView unload",
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
                _visibleArtistIndexes.Clear();

                // If any cleanup was missed in OnUnloaded, handle it here
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error detaching ArtistsView",
                    "Failed to properly clean up resources when detaching from visual tree.",
                    ex,
                    false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}