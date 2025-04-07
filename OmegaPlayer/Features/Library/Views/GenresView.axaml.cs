using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class GenresView : UserControl
    {
        private ItemsControl _genresItemsControl;
        private HashSet<int> _visibleGenreIndexes = new HashSet<int>();

        public GenresView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Hook into the Loaded event to find the ItemsControl
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _genresItemsControl = this.FindControl<ItemsControl>("GenresItemsControl");

            // Check initially visible items
            if (_genresItemsControl != null)
            {
                // Delay slightly to ensure containers are realized
                Dispatcher.UIThread.Post(() => CheckVisibleItems(null));
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                var scrollViewer = sender as ScrollViewer;

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    // Get the current view's ViewModel
                    if (DataContext is GenresViewModel genresViewModel &&
                        genresViewModel is ILoadMoreItems loadMoreItems &&
                        loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                    {
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }

                // Also check for visibility changes
                CheckVisibleItems(scrollViewer);
            }
        }

        private async void CheckVisibleItems(ScrollViewer scrollViewer)
        {
            if (DataContext is not GenresViewModel viewModel || _genresItemsControl == null)
                return;

            // Ensure we have a ScrollViewer (might be null when initially called)
            if (scrollViewer == null)
            {
                scrollViewer = this.FindControl<ScrollViewer>("GenresScrollViewer");
                if (scrollViewer == null) return;
            }

            // Keep track of which items are currently visible
            var newVisibleIndexes = new HashSet<int>();

            // Get all item containers
            var containers = _genresItemsControl.GetRealizedContainers();

            foreach (var container in containers)
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
                    int index = _genresItemsControl.IndexFromContainer(container);

                    if (isVisible)
                    {
                        newVisibleIndexes.Add(index);

                        // If not previously visible, notify it's now visible
                        if (!_visibleGenreIndexes.Contains(index))
                        {
                            // Get the genre from the ViewModel
                            if (index >= 0 && index < viewModel.Genres.Count)
                            {
                                var genre = viewModel.Genres[index];
                                await viewModel.NotifyGenreVisible(genre, true);
                            }
                        }
                    }
                    else if (_visibleGenreIndexes.Contains(index))
                    {
                        // Was visible before but not anymore
                        if (index >= 0 && index < viewModel.Genres.Count)
                        {
                            var genre = viewModel.Genres[index];
                            await viewModel.NotifyGenreVisible(genre, false);
                        }
                    }
                }
            }

            // Update the visible indexes
            _visibleGenreIndexes = newVisibleIndexes;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Clean up event handlers
            Loaded -= OnLoaded;

            base.OnDetachedFromVisualTree(e);
        }
    }
}