using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Search.ViewModels;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Search.Views
{
    public partial class SearchView : UserControl
    {
        private ScrollViewer _mainScrollViewer;
        private ItemsControl _tracksItemsControl;
        private ItemsControl _albumsItemsControl;
        private ItemsControl _artistsItemsControl;

        // Track which items are currently visible
        private HashSet<int> _visibleTrackIndexes = new HashSet<int>();
        private HashSet<int> _visibleAlbumIndexes = new HashSet<int>();
        private HashSet<int> _visibleArtistIndexes = new HashSet<int>();

        private SearchViewModel ViewModel => DataContext as SearchViewModel;

        public SearchView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Wait for UI to load then capture references
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Find the main ScrollViewer
            _mainScrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (_mainScrollViewer != null)
            {
                _mainScrollViewer.ScrollChanged += OnScrollChanged;
            }

            // Find the ItemsControls
            _tracksItemsControl = this.FindControl<ItemsControl>("TracksItemsControl");
            _albumsItemsControl = this.FindControl<ItemsControl>("AlbumsItemsControl");
            _artistsItemsControl = this.FindControl<ItemsControl>("ArtistsItemsControl");

            // Check initial visibility after a short delay to ensure UI is rendered
            Dispatcher.UIThread.Post(CheckVisibleItems, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // When scrolling occurs, check which items are now visible
            CheckVisibleItems();
        }

        private async void CheckVisibleItems()
        {
            if (ViewModel == null || _mainScrollViewer == null) return;

            // Check visible tracks
            if (_tracksItemsControl != null)
            {
                await CheckVisibleItemsInControl(
                    _tracksItemsControl,
                    ViewModel.Tracks,
                    _visibleTrackIndexes,
                    (item, visible) => ViewModel.NotifyTrackVisible(item as TrackDisplayModel, visible));
            }

            // Check visible albums
            if (_albumsItemsControl != null)
            {
                await CheckVisibleItemsInControl(
                    _albumsItemsControl,
                    ViewModel.Albums,
                    _visibleAlbumIndexes,
                    (item, visible) => ViewModel.NotifyAlbumVisible(item as AlbumDisplayModel, visible));
            }

            // Check visible artists
            if (_artistsItemsControl != null)
            {
                await CheckVisibleItemsInControl(
                    _artistsItemsControl,
                    ViewModel.Artists,
                    _visibleArtistIndexes,
                    (item, visible) => ViewModel.NotifyArtistVisible(item as ArtistDisplayModel, visible));
            }
        }

        private async System.Threading.Tasks.Task CheckVisibleItemsInControl<T>(
            ItemsControl itemsControl,
            IList<T> items,
            HashSet<int> visibleIndexes,
            System.Func<object, bool, System.Threading.Tasks.Task> notifyCallback)
        {
            if (items == null || items.Count == 0) return;

            var newVisibleIndexes = new HashSet<int>();
            var containers = itemsControl.GetRealizedContainers();

            foreach (var container in containers)
            {
                // Get the container's position relative to the scroll viewer
                var transform = container.TransformToVisual(_mainScrollViewer);
                if (transform != null)
                {
                    var containerTop = transform.Value.Transform(new Point(0, 0)).Y;
                    var containerHeight = container.Bounds.Height;
                    var containerBottom = containerTop + containerHeight;

                    // Check if the container is in the viewport (fully or partially)
                    bool isVisible = (containerBottom > 0 && containerTop < _mainScrollViewer.Viewport.Height);

                    // Get the container's index
                    int index = itemsControl.IndexFromContainer(container);

                    if (isVisible)
                    {
                        newVisibleIndexes.Add(index);

                        // If not previously visible, notify it's now visible
                        if (!visibleIndexes.Contains(index))
                        {
                            if (index >= 0 && index < items.Count)
                            {
                                var item = items[index];
                                await notifyCallback(item, true);
                            }
                        }
                    }
                    else if (visibleIndexes.Contains(index))
                    {
                        // Was visible before but not anymore
                        if (index >= 0 && index < items.Count)
                        {
                            var item = items[index];
                            await notifyCallback(item, false);
                        }
                    }
                }
            }

            // Update the visible indexes
            visibleIndexes.Clear();
            foreach (var index in newVisibleIndexes)
            {
                visibleIndexes.Add(index);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Clean up event handlers
            if (_mainScrollViewer != null)
            {
                _mainScrollViewer.ScrollChanged -= OnScrollChanged;
            }

            Loaded -= OnLoaded;

            base.OnDetachedFromVisualTree(e);
        }
    }
}