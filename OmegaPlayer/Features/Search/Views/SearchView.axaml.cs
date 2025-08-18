using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Search.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Helpers;
using System;
using System.Linq;

namespace OmegaPlayer.Features.Search.Views
{
    public partial class SearchView : UserControl
    {
        private ScrollViewer _searchScrollViewer;
        private ItemsRepeater _tracksItemsRepeater;
        private ItemsRepeater _albumsItemsRepeater;
        private ItemsRepeater _artistsItemsRepeater;
        private IErrorHandlingService _errorHandlingService;
        private DispatcherTimer _visibilityCheckTimer;
        private bool _isDisposed = false;

        private WindowResizeHandler _windowResizeHandler;
        private bool _isResizeInProgress = false;
        private double _storedScrollPosition = 0;
        private bool _layoutUpdatesPaused = false;
        private double _storedScrollPercentage = 0.0;
        private double _storedContentHeight = 0.0;

        private SearchViewModel ViewModel => DataContext as SearchViewModel;

        public SearchView()
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
                if (!_layoutUpdatesPaused) // Don't check visibility during resize
                {
                    CheckVisibleItems();
                }
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    _searchScrollViewer = this.FindControl<ScrollViewer>("SearchScrollViewer");
                    _tracksItemsRepeater = this.FindControl<ItemsRepeater>("TracksItemsRepeater");
                    _albumsItemsRepeater = this.FindControl<ItemsRepeater>("AlbumsItemsRepeater");
                    _artistsItemsRepeater = this.FindControl<ItemsRepeater>("ArtistsItemsRepeater");

                    if (ViewModel != null)
                    {
                        ViewModel.TriggerTracksVisibilityCheck = () => CheckVisibleTracks();
                        ViewModel.TriggerAlbumsVisibilityCheck = () => CheckVisibleAlbums();
                        ViewModel.TriggerArtistsVisibilityCheck = () => CheckVisibleArtists();
                    }
                },
                "Loading search view controls",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void SearchScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isDisposed || _layoutUpdatesPaused) return;

            // Use timer-based batching to reduce excessive calls during fast scrolling
            _visibilityCheckTimer.Stop();
            _visibilityCheckTimer.Start();
        }

        private void CheckVisibleItems()
        {
            CheckVisibleTracks();
            CheckVisibleAlbums();
            CheckVisibleArtists();
        }

        private async void CheckVisibleTracks()
        {
            if (_isDisposed || _searchScrollViewer == null || _tracksItemsRepeater == null || ViewModel == null) return;

            try
            {
                var (itemHeight, itemWidth, itemsPerRow) = GetTrackDimensions(_searchScrollViewer.Viewport.Width);
                var viewportTop = _searchScrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _searchScrollViewer.Viewport.Height;
                var buffer = 300;

                int index = 0;
                foreach (var track in ViewModel.Tracks)
                {
                    if (track.Thumbnail == null)
                    {
                        var rowIndex = index / itemsPerRow;
                        var estimatedTop = rowIndex * itemHeight;
                        var estimatedBottom = estimatedTop + itemHeight;

                        bool isVisible = estimatedBottom > (viewportTop - buffer) && estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            await ViewModel.NotifyTrackVisible(track, true);
                        }
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error checking visible tracks", ex.Message, ex, false);
            }
        }

        private async void CheckVisibleAlbums()
        {
            if (_isDisposed || _searchScrollViewer == null || _albumsItemsRepeater == null || ViewModel == null) return;

            try
            {
                var (itemHeight, itemWidth, itemsPerRow) = GetAlbumDimensions(_searchScrollViewer.Viewport.Width);
                var viewportTop = _searchScrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _searchScrollViewer.Viewport.Height;
                var buffer = 300;

                int index = 0;
                foreach (var album in ViewModel.Albums)
                {
                    if (album.Cover == null)
                    {
                        var rowIndex = index / itemsPerRow;
                        var estimatedTop = rowIndex * itemHeight;
                        var estimatedBottom = estimatedTop + itemHeight;

                        bool isVisible = estimatedBottom > (viewportTop - buffer) && estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            await ViewModel.NotifyAlbumVisible(album, true);
                        }
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error checking visible albums", ex.Message, ex, false);
            }
        }

        private async void CheckVisibleArtists()
        {
            if (_isDisposed || _searchScrollViewer == null || _artistsItemsRepeater == null || ViewModel == null) return;

            try
            {
                var (itemHeight, itemWidth, itemsPerRow) = GetArtistDimensions(_searchScrollViewer.Viewport.Width);
                var viewportTop = _searchScrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _searchScrollViewer.Viewport.Height;
                var buffer = 300;

                int index = 0;
                foreach (var artist in ViewModel.Artists)
                {
                    if (artist.Photo == null)
                    {
                        var rowIndex = index / itemsPerRow;
                        var estimatedTop = rowIndex * itemHeight;
                        var estimatedBottom = estimatedTop + itemHeight;

                        bool isVisible = estimatedBottom > (viewportTop - buffer) && estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            await ViewModel.NotifyArtistVisible(artist, true);
                        }
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error checking visible artists", ex.Message, ex, false);
            }
        }

        private (double itemHeight, double itemWidth, int itemsPerRow) GetTrackDimensions(double viewportWidth)
        {
            var actualDimensions = GetActualItemDimensions(_tracksItemsRepeater, viewportWidth);
            return actualDimensions ?? (190, 152, Math.Max(1, (int)(viewportWidth / 152)));
        }

        private (double itemHeight, double itemWidth, int itemsPerRow) GetAlbumDimensions(double viewportWidth)
        {
            var actualDimensions = GetActualItemDimensions(_albumsItemsRepeater, viewportWidth);
            return actualDimensions ?? (190, 152, Math.Max(1, (int)(viewportWidth / 152)));
        }

        private (double itemHeight, double itemWidth, int itemsPerRow) GetArtistDimensions(double viewportWidth)
        {
            var actualDimensions = GetActualItemDimensions(_artistsItemsRepeater, viewportWidth);
            return actualDimensions ?? (190, 152, Math.Max(1, (int)(viewportWidth / 152)));
        }

        private (double itemHeight, double itemWidth, int itemsPerRow)? GetActualItemDimensions(ItemsRepeater repeater, double viewportWidth)
        {
            if (repeater == null) return null;

            try
            {
                var containers = repeater.GetVisualChildren().OfType<Control>().Where(c => c.DataContext != null).Take(5).ToList();
                if (containers.Count == 0) return null;

                var avgHeight = containers.Average(c => c.Bounds.Height);
                var avgWidth = containers.Average(c => c.Bounds.Width);
                var itemsPerRow = Math.Max(1, (int)(viewportWidth / avgWidth));

                return (avgHeight, avgWidth, itemsPerRow);
            }
            catch { return null; }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            try
            {
                var mainView = this.FindAncestorOfType<MainView>();
                if (mainView != null)
                {
                    _windowResizeHandler = mainView.ResizeHandler;
                    _windowResizeHandler?.RegisterSearchView(this);
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error registering SearchView with WindowResizeHandler", ex.Message, ex, false);
            }
        }

        private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            try
            {
                _windowResizeHandler?.UnregisterSearchView(this);
                _windowResizeHandler = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error unregistering SearchView from WindowResizeHandler", ex.Message, ex, false);
            }
        }

        public void OnResizeStarted()
        {
            try
            {
                if (_searchScrollViewer == null || _isResizeInProgress) return;

                _isResizeInProgress = true;
                _layoutUpdatesPaused = true;

                _storedScrollPosition = _searchScrollViewer.Offset.Y;
                _storedContentHeight = _searchScrollViewer.Extent.Height;

                if (_storedContentHeight > 0)
                {
                    _storedScrollPercentage = _storedScrollPosition / _storedContentHeight;
                }
                else
                {
                    _storedScrollPercentage = 0.0;
                }

                _visibilityCheckTimer?.Stop();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize start in SearchView", ex.Message, ex, false);
            }
        }

        public void OnResizeCompleted()
        {
            try
            {
                if (!_isResizeInProgress) return;

                _isResizeInProgress = false;
                _layoutUpdatesPaused = false;

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        InvalidateArrange();
                        InvalidateMeasure();

                        Dispatcher.UIThread.Post(() =>
                        {
                            RestoreScrollPosition();
                            CheckVisibleItems();
                        }, DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error in deferred layout update", ex.Message, ex, false);
                    }
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize completion in SearchView", ex.Message, ex, false);
            }
        }

        private void RestoreScrollPosition()
        {
            if (_searchScrollViewer == null) return;

            try
            {
                // Strategy 1: Layout-independent percentage-based restoration
                if (_storedScrollPercentage >= 0 && _searchScrollViewer.Extent.Height > 0)
                {
                    var targetPosition = _storedScrollPercentage * _searchScrollViewer.Extent.Height;
                    var clampedPosition = Math.Max(0,
                        Math.Min(targetPosition,
                                _searchScrollViewer.Extent.Height - _searchScrollViewer.Viewport.Height));

                    _searchScrollViewer.Offset = new Vector(_searchScrollViewer.Offset.X, clampedPosition);
                    return;
                }

                // Fallback Strategy: Direct pixel restoration
                var fallbackPosition = Math.Max(0,
                    Math.Min(_storedScrollPosition,
                            _searchScrollViewer.Extent.Height - _searchScrollViewer.Viewport.Height));

                _searchScrollViewer.Offset = new Vector(_searchScrollViewer.Offset.X, fallbackPosition);
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error restoring scroll position", ex.Message, ex, false);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isDisposed = true;
                _visibilityCheckTimer?.Stop();
                _visibilityCheckTimer = null;
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                _windowResizeHandler?.UnregisterSearchView(this);

                _tracksItemsRepeater = null;
                _albumsItemsRepeater = null;
                _artistsItemsRepeater = null;
                _searchScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error during SearchView unload", ex.Message, ex, false);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                _isDisposed = true;
                _visibilityCheckTimer?.Stop();
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;

                _windowResizeHandler?.UnregisterPlaylistView(this);

                _windowResizeHandler = null;
                _tracksItemsRepeater = null;
                _albumsItemsRepeater = null;
                _artistsItemsRepeater = null;
                _searchScrollViewer = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error detaching SearchView", ex.Message, ex, false);
            }

            base.OnDetachedFromVisualTree(e);
        }
    }
}