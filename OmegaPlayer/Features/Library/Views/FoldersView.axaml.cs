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
    public partial class FoldersView : UserControl
    {
        private ItemsControl _foldersItemsControl;
        private HashSet<int> _visibleFolderIndexes = new HashSet<int>();

        public FoldersView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Hook into the Loaded event to find the ItemsControl
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _foldersItemsControl = this.FindControl<ItemsControl>("FoldersItemsControl");

            // Check initially visible items
            if (_foldersItemsControl != null)
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
                    if (DataContext is FoldersViewModel folderViewModel &&
                        folderViewModel is ILoadMoreItems loadMoreItems &&
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
            if (DataContext is not FoldersViewModel viewModel || _foldersItemsControl == null)
                return;

            // Ensure we have a ScrollViewer (might be null when initially called)
            if (scrollViewer == null)
            {
                scrollViewer = this.FindControl<ScrollViewer>("FoldersScrollViewer");
                if (scrollViewer == null) return;
            }

            // Keep track of which items are currently visible
            var newVisibleIndexes = new HashSet<int>();

            // Get all item containers
            var containers = _foldersItemsControl.GetRealizedContainers();

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
                    int index = _foldersItemsControl.IndexFromContainer(container);

                    if (isVisible)
                    {
                        newVisibleIndexes.Add(index);

                        // If not previously visible, notify it's now visible
                        if (!_visibleFolderIndexes.Contains(index))
                        {
                            // Get the folder from the ViewModel
                            if (index >= 0 && index < viewModel.Folders.Count)
                            {
                                var folder = viewModel.Folders[index];
                                await viewModel.NotifyFolderVisible(folder, true);
                            }
                        }
                    }
                    else if (_visibleFolderIndexes.Contains(index))
                    {
                        // Was visible before but not anymore
                        if (index >= 0 && index < viewModel.Folders.Count)
                        {
                            var folder = viewModel.Folders[index];
                            await viewModel.NotifyFolderVisible(folder, false);
                        }
                    }
                }
            }

            // Update the visible indexes
            _visibleFolderIndexes = newVisibleIndexes;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Clean up event handlers
            Loaded -= OnLoaded;

            base.OnDetachedFromVisualTree(e);
        }
    }
}