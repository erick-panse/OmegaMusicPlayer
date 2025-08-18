using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Helpers;
using System;
using System.Linq;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class AlbumsView : UserControl
    {
        private ItemsRepeater _albumsItemsRepeater;
        private ScrollViewer _scrollViewer;
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;
        private DispatcherTimer _visibilityCheckTimer;

        private WindowResizeHandler _windowResizeHandler;
        private bool _isResizeInProgress = false;
        private double _storedScrollPosition = 0;
        private bool _layoutUpdatesPaused = false;
        private double _storedScrollPercentage = 0.0;
        private double _storedContentHeight = 0.0;

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
            _albumsItemsRepeater = this.FindControl<ItemsRepeater>("AlbumsItemsRepeater");
            _scrollViewer = this.FindControl<ScrollViewer>("AlbumsScrollViewer");
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isDisposed || _layoutUpdatesPaused) return;

            // Use timer-based batching to reduce excessive calls during fast scrolling
            _visibilityCheckTimer.Stop();
            _visibilityCheckTimer.Start();
        }

        private async void CheckVisibleItems()
        {
            if (_isDisposed || _scrollViewer == null || _albumsItemsRepeater == null) return;
            if (!(DataContext is AlbumsViewModel viewModel)) return;

            try
            {
                var viewportTop = _scrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _scrollViewer.Viewport.Height;
                var buffer = 300;

                var (itemHeight, itemWidth, itemsPerRow) = GetItemDimensions(_scrollViewer.Viewport.Width);

                int index = 0;
                foreach (var album in viewModel.Albums)
                {
                    if (album.Cover == null)
                    {
                        var rowIndex = index / itemsPerRow;
                        var estimatedTop = rowIndex * itemHeight;
                        var estimatedBottom = estimatedTop + itemHeight;

                        bool isVisible = estimatedBottom > (viewportTop - buffer) && estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            await viewModel.NotifyAlbumVisible(album, true);
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

        private (double itemHeight, double itemWidth, int itemsPerRow) GetItemDimensions(double viewportWidth)
        {
            var actualDimensions = GetActualItemDimensions(viewportWidth);
            return actualDimensions ?? (190, 152, Math.Max(1, (int)(viewportWidth / 152)));
        }

        private (double itemHeight, double itemWidth, int itemsPerRow)? GetActualItemDimensions(double viewportWidth)
        {
            if (_albumsItemsRepeater == null) return null;

            try
            {
                var containers = _albumsItemsRepeater.GetVisualChildren().OfType<Control>().Where(c => c.DataContext != null).Take(5).ToList();
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
                    _windowResizeHandler?.RegisterAlbumView(this);
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error registering AlbumsView with WindowResizeHandler", ex.Message, ex, false);
            }
        }

        private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            try
            {
                _windowResizeHandler?.UnregisterAlbumView(this);
                _windowResizeHandler = null;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error unregistering AlbumsView from WindowResizeHandler", ex.Message, ex, false);
            }
        }

        public void OnResizeStarted()
        {
            try
            {
                if (_scrollViewer == null || _isResizeInProgress) return;

                _isResizeInProgress = true;
                _layoutUpdatesPaused = true;

                _storedScrollPosition = _scrollViewer.Offset.Y;
                _storedContentHeight = _scrollViewer.Extent.Height;

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
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize start in AlbumsView", ex.Message, ex, false);
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
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize completion in AlbumsView", ex.Message, ex, false);
            }
        }

        private void RestoreScrollPosition()
        {
            if (_scrollViewer == null) return;

            try
            {
                // Strategy 1: Layout-independent percentage-based restoration
                if (_storedScrollPercentage >= 0 && _scrollViewer.Extent.Height > 0)
                {
                    var targetPosition = _storedScrollPercentage * _scrollViewer.Extent.Height;
                    var clampedPosition = Math.Max(0,
                        Math.Min(targetPosition,
                                _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height));

                    _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, clampedPosition);
                    return;
                }

                // Fallback Strategy: Direct pixel restoration
                var fallbackPosition = Math.Max(0,
                    Math.Min(_storedScrollPosition,
                            _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height));

                _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, fallbackPosition);
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error restoring scroll position", ex.Message, ex, false);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isDisposed = true;
            _visibilityCheckTimer?.Stop();
            _visibilityCheckTimer = null;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;

            _windowResizeHandler?.UnregisterAlbumView(this);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;
            _visibilityCheckTimer?.Stop();

            _windowResizeHandler?.UnregisterPlaylistView(this);
            _windowResizeHandler = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}