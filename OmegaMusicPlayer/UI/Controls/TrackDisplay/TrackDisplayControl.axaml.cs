using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.LibraryEnums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.ViewModels;
using OmegaMusicPlayer.Features.Shell.Views;
using OmegaMusicPlayer.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.UI.Controls.TrackDisplay
{
    public partial class TrackDisplayControl : TemplatedControl
    {
        public static readonly StyledProperty<ViewType> ViewTypeProperty =
            AvaloniaProperty.Register<TrackDisplayControl, ViewType>(
                nameof(ViewType),
                ViewType.Card);

        public ViewType ViewType
        {
            get => GetValue(ViewTypeProperty);
            set => SetValue(ViewTypeProperty, value);
        }

        public static readonly StyledProperty<System.Collections.IEnumerable> ItemsSourceProperty =
            AvaloniaProperty.Register<TrackDisplayControl, System.Collections.IEnumerable>(
                nameof(ItemsSource));

        public System.Collections.IEnumerable ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private IMessenger _messenger;
        private IErrorHandlingService _errorHandlingService;
        private ScrollViewer _scrollViewer;
        private ItemsRepeater _itemsRepeater;
        private const double AutoScrollThreshold = 50.0; // Pixels from edge to trigger auto-scroll
        private const double AutoScrollSpeed = 15.0; // Pixels per frame
        private bool _isAutoScrolling;
        private Point _lastDragPosition;
        private bool _isDisposed = false;
        private bool _autoScrollTaskRunning = false;
        private DispatcherTimer _visibilityCheckTimer;

        // Resize coordination properties
        private WindowResizeHandler _windowResizeHandler;
        private bool _isResizeInProgress = false;
        private double _storedScrollPosition = 0;
        private bool _layoutUpdatesPaused = false;
        private int _storedFirstVisibleIndex = -1;
        private double _storedScrollPercentage = 0.0;
        private double _storedContentHeight = 0.0;

        public TrackDisplayControl()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();
            _messenger = App.ServiceProvider?.GetService<IMessenger>();

            InitializeComponent();

            // Register for scroll message
            _messenger?.Register<ScrollToTrackMessage>(this, (r, m) => ScrollToTrack(m.TrackIndex));

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
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // Call base implementation
            base.OnApplyTemplate(e);

            // Clean up old event handlers if this is a re-template
            DetachEventHandlers();

            // Get reference to ScrollViewer DetailsScrollViewer - Without correct reference Reorder will not work
            Visual current = this;
            while (current != null)
            {
                if (current is ScrollViewer svDetails && svDetails.Name == "DetailsScrollViewer")
                {
                    _scrollViewer = svDetails;
                    break;
                }
                else if (current is ScrollViewer svLibrary && svLibrary.Name == "LibraryScrollViewer")
                {
                    _scrollViewer = svLibrary;
                    break;
                }
                current = current.GetVisualParent();
            }

            // Find main ScrollViewer in template if DetailsScrollViewer not found
            if (_scrollViewer == null)
            {
                _scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            }

            // Find the items control and attach event handlers
            _itemsRepeater = e.NameScope.Find<ItemsRepeater>("TracksItemsRepeater");
            if (_itemsRepeater != null)
            {
                // Add scroll change handler for all view types
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged += OnScrollChanged;
                }

                _itemsRepeater.AddHandler(InputElement.PointerReleasedEvent, ArtistClicked, RoutingStrategies.Tunnel);
                _itemsRepeater.AddHandler(InputElement.PointerEnteredEvent, Track_PointerEntered, RoutingStrategies.Tunnel);
                _itemsRepeater.AddHandler(InputElement.PointerExitedEvent, Track_PointerExited, RoutingStrategies.Tunnel);

                // Add drag-drop handlers at the container level
                _itemsRepeater.AddHandler(DragDrop.DragOverEvent, Track_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                _itemsRepeater.AddHandler(DragDrop.DropEvent, Track_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                _itemsRepeater.AddHandler(InputElement.PointerPressedEvent, Track_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

                // Enable drag-drop on the ItemsRepeater itself
                DragDrop.SetAllowDrop(_itemsRepeater, true);

                // Add pointer wheel handler for scrolling while dragging
                _itemsRepeater.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

                // Initial check for visible items (only if not in resize)
                if (!_layoutUpdatesPaused)
                {
                    Dispatcher.UIThread.Post(() => CheckVisibleItems(), DispatcherPriority.Background);
                }
            }
            else
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "ItemsRepeater not found",
                    "TracksItemsRepeater not found in template for TrackDisplayControl",
                    null,
                    false);
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_layoutUpdatesPaused) return; // Don't process scroll events during resize

            _scrollViewer = sender as ScrollViewer;

            // Use timer-based batching to reduce excessive calls during fast scrolling
            _visibilityCheckTimer.Stop();
            _visibilityCheckTimer.Start();
        }

        private async void ScrollToTrack(int trackIndex)
        {
            if (_isDisposed || _scrollViewer == null || trackIndex < 0) return;

            try
            {
                // Get total items from viewmodel
                var totalItems = 0;
                if (DataContext is DetailsViewModel detailsVM)
                {
                    totalItems = detailsVM.AllTracks?.Count ?? 0;
                }

                if (totalItems == 0) return;

                var viewportWidth = _scrollViewer.Viewport.Width - 20; // get Width and subtract side margins (20px)

                // Calculate dimensions with margins (each track has 3px margin = 6px total spacing)
                double itemHeight, itemWidth;
                int itemsPerRow;

                if (ViewType == ViewType.List)
                {
                    itemHeight = 55 + 4; // List has smaller height margins
                    itemWidth = viewportWidth;
                    itemsPerRow = 1;
                }
                else if (ViewType == ViewType.Card)
                {
                    itemHeight = 210 + 6; // base height + margins  
                    itemWidth = 152 + 6; // base width + margins
                    itemsPerRow = (int)(viewportWidth / itemWidth);
                }
                else if (ViewType == ViewType.Image)
                {
                    itemHeight = 148 + 6;
                    itemWidth = 148 + 6;
                    itemsPerRow = (int)(viewportWidth / itemWidth);
                }
                else // RoundImage
                {
                    itemHeight = 170 + 6;
                    itemWidth = 140 + 6;
                    itemsPerRow = (int)(viewportWidth / itemWidth);
                }

                // Ensure at least 1 item per row
                if (itemsPerRow < 1) itemsPerRow = 1;

                // Calculate target position
                double targetPosition;
                if (ViewType == ViewType.List)
                {
                    targetPosition = trackIndex * itemHeight;
                }
                else
                {
                    int rowIndex = trackIndex / itemsPerRow;
                    targetPosition = rowIndex * itemHeight;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, targetPosition);
                });
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error scrolling to track", ex.Message, ex, false);
            }
        }

        private async void CheckVisibleItems()
        {
            if (_isDisposed || _scrollViewer == null || ItemsSource == null || _layoutUpdatesPaused) return;

            try
            {
                var viewportTop = _scrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _scrollViewer.Viewport.Height;
                var buffer = 300; // Preload buffer

                // Get viewport width for calculations
                var viewportWidth = _scrollViewer.Viewport.Width;

                // Calculate dimensions and layout based on ViewType
                var (itemHeight, itemWidth, itemsPerRow) = GetItemDimensions(viewportWidth);

                int index = 0;
                foreach (var item in ItemsSource)
                {
                    if (item is TrackDisplayModel track && track.Thumbnail == null)
                    {
                        var (estimatedTop, estimatedBottom) = CalculateItemPosition(index, itemHeight, itemsPerRow);

                        bool isVisible = estimatedBottom > (viewportTop - buffer) &&
                                       estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            if (DataContext is LibraryViewModel libraryVM)
                            {
                                await libraryVM.NotifyTrackVisible(track, true);
                            }
                            else if (DataContext is DetailsViewModel detailsVM)
                            {
                                await detailsVM.NotifyTrackVisible(track, true);
                            }
                        }
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error checking visible items", ex.Message, ex, false);
            }
        }

        private (double itemHeight, double itemWidth, int itemsPerRow) GetItemDimensions(double viewportWidth)
        {
            // Try to get actual dimensions from realized containers first
            var actualDimensions = GetActualItemDimensions(viewportWidth);
            if (actualDimensions.HasValue)
            {
                return actualDimensions.Value;
            }

            // Fallback to estimated dimensions
            return ViewType switch
            {
                ViewType.List => (55, viewportWidth, 1),
                ViewType.Card => (210, 152, Math.Max(1, (int)(viewportWidth / 152))),
                ViewType.Image => (148, 148, Math.Max(1, (int)(viewportWidth / 148))),
                ViewType.RoundImage => (170, 140, Math.Max(1, (int)(viewportWidth / 140))),
                _ => (55, viewportWidth, 1)
            };
        }

        private (double itemHeight, double itemWidth, int itemsPerRow)? GetActualItemDimensions(double viewportWidth)
        {
            if (_itemsRepeater == null) return null;

            try
            {
                // Find realized containers in the visual tree
                var containers = FindRealizedContainers(_itemsRepeater);
                if (containers.Count == 0) return null;

                // Measure first few containers to get average dimensions
                var heights = new List<double>();
                var widths = new List<double>();

                foreach (var container in containers.Take(5)) // Sample first 5 containers
                {
                    if (container.Bounds.Width > 0 && container.Bounds.Height > 0)
                    {
                        heights.Add(container.Bounds.Height);
                        widths.Add(container.Bounds.Width);
                    }
                }

                if (heights.Count == 0) return null;

                var avgHeight = heights.Average();
                var avgWidth = widths.Average();

                // Calculate items per row based on actual width and viewport
                int itemsPerRow = ViewType == ViewType.List ? 1 :
                                 Math.Max(1, (int)(viewportWidth / avgWidth));

                return (avgHeight, avgWidth, itemsPerRow);
            }
            catch
            {
                return null;
            }
        }

        private List<Control> FindRealizedContainers(ItemsRepeater repeater)
        {
            var containers = new List<Control>();

            // Traverse visual children to find realized containers
            var children = repeater.GetVisualChildren().ToList();

            foreach (var child in children)
            {
                if (child is Control container && container.DataContext != null)
                {
                    containers.Add(container);
                }
            }

            return containers;
        }

        private (double top, double bottom) CalculateItemPosition(int index, double itemHeight, int itemsPerRow)
        {
            if (ViewType == ViewType.List)
            {
                // Vertical stack layout
                var top = index * itemHeight;
                return (top, top + itemHeight);
            }
            else
            {
                // Wrap layout
                var rowIndex = index / itemsPerRow;
                var top = rowIndex * itemHeight;
                return (top, top + itemHeight);
            }
        }

        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (_isDisposed || _scrollViewer == null) return;

            if (DataContext is DetailsViewModel viewModel && viewModel.DraggedTrack != null)
            {
                _isAutoScrolling = false;
                // Handle scroll during drag
                var delta = e.Delta.Y * 50;
                _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, _scrollViewer.Offset.Y - delta);
                e.Handled = true;
            }
        }

        private async Task AutoScroll(double scrollAmount)
        {
            // Prevent multiple auto-scroll tasks from running
            if (_autoScrollTaskRunning) return;

            _autoScrollTaskRunning = true;

            while (_isAutoScrolling && !_isDisposed && _scrollViewer != null)
            {
                _scrollViewer.Offset = new Vector(
                    _scrollViewer.Offset.X,
                    Math.Clamp(_scrollViewer.Offset.Y + scrollAmount, 0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height)
                );
                await Task.Delay(16); // Approximately 60 FPS
            }

            _autoScrollTaskRunning = false;
        }

        private async void Track_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (_isDisposed) return;

            // Find the track container
            var element = e.Source as Visual;
            while (element != null && !(element is Border border && border.Name == "TrackContainer"))
            {
                element = element.GetVisualParent();
            }

            if (element is Border trackContainer &&
                trackContainer.DataContext is TrackDisplayModel track &&
                DataContext is DetailsViewModel viewModel &&
                viewModel.IsReorderMode)
            {
                // Create drag data
                var data = new DataObject();
                data.Set(DataFormats.Text, track.TrackID.ToString());

                viewModel.HandleTrackDragStarted(track);

                // Start the drag operation
                var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
        }

        private async void Track_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (_isDisposed || _scrollViewer == null || !(DataContext is DetailsViewModel viewModel)) return;

                // Store current drag position for auto-scroll
                _lastDragPosition = e.GetPosition(_scrollViewer);

                // Auto-scroll logic (same as before)
                double distanceFromTop = _lastDragPosition.Y;
                double distanceFromBottom = _scrollViewer.Bounds.Height - _lastDragPosition.Y;

                bool shouldScrollUp = distanceFromTop < AutoScrollThreshold;
                bool shouldScrollDown = distanceFromBottom < AutoScrollThreshold;

                if ((shouldScrollUp || shouldScrollDown) && !_isAutoScrolling)
                {
                    _isAutoScrolling = true;
                    await AutoScroll(shouldScrollUp ? -AutoScrollSpeed : AutoScrollSpeed);
                }
                else if (!shouldScrollUp && !shouldScrollDown)
                {
                    _isAutoScrolling = false;
                }

                // Find the track container
                var element = e.Source as Visual;
                while (element != null &&
                       !(element is Border border &&
                         (border.Name == "TrackContainer" || border.Name == "TrackOuterContainer")))
                {
                    element = element.GetVisualParent();
                }

                if (element is Border container)
                {
                    var track = container.DataContext as TrackDisplayModel;
                    if (track == null && container.Parent != null)
                    {
                        track = (container.Parent as StyledElement)?.DataContext as TrackDisplayModel;
                    }

                    if (track != null)
                    {
                        // Get index from ItemsSource
                        int index = GetTrackIndex(track);
                        if (index >= 0)
                        {
                            e.DragEffects = DragDropEffects.Move;
                            viewModel.HandleTrackDragOver(index);
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error in Track_DragOver", ex.Message, ex, false);
                _isAutoScrolling = false;
            }
        }

        private int GetTrackIndex(TrackDisplayModel track)
        {
            if (ItemsSource is IList<TrackDisplayModel> list)
            {
                return list.IndexOf(track);
            }
            else if (ItemsSource is System.Collections.IEnumerable items)
            {
                int index = 0;
                foreach (var item in items)
                {
                    if (item == track)
                        return index;
                    index++;
                }
            }
            return -1;
        }

        private void Track_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (_isDisposed) return;

                _isAutoScrolling = false;
                if (DataContext is DetailsViewModel viewModel)
                {
                    viewModel.HandleTrackDrop();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling track drop in TrackDisplayControl",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void ArtistClicked(object sender, PointerReleasedEventArgs e)
        {
            try
            {
                if (_isDisposed) return;

                if (e.Source is TextBlock textBlock &&
                    textBlock.Name == "ArtistTextBlock" &&
                    textBlock.Tag is Artists artist &&
                    DataContext is LibraryViewModel viewModel)
                {
                    viewModel.OpenArtistCommand.Execute(artist);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling artist click in TrackDisplayControl",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void Track_PointerEntered(object sender, PointerEventArgs e)
        {
            if (_isDisposed) return;

            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = true;
            }
        }

        private void Track_PointerExited(object sender, PointerEventArgs e)
        {
            if (_isDisposed) return;

            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = false;
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            try
            {
                // Find the MainView window and register with its WindowResizeHandler
                var mainView = this.FindAncestorOfType<MainView>();
                if (mainView != null)
                {
                    _windowResizeHandler = mainView.ResizeHandler;
                    _windowResizeHandler?.RegisterTrackDisplayControl(this);
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error registering with WindowResizeHandler",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Called by WindowResizeHandler when a resize operation starts
        /// </summary>
        public void OnResizeStarted()
        {
            try
            {
                if (_scrollViewer == null || _isResizeInProgress) return;

                _isResizeInProgress = true;
                _layoutUpdatesPaused = true;

                // Store current scroll position
                _storedScrollPosition = _scrollViewer.Offset.Y;
                _storedContentHeight = _scrollViewer.Extent.Height;

                // Calculate scroll percentage (layout-independent)
                if (_storedContentHeight > 0)
                {
                    _storedScrollPercentage = _storedScrollPosition / _storedContentHeight;
                }
                else
                {
                    _storedScrollPercentage = 0.0;
                }

                // Store the index of the first visible item for better restoration
                _storedFirstVisibleIndex = GetFirstVisibleItemIndex();

                // Stop visibility checks during resize
                _visibilityCheckTimer?.Stop();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling resize start in TrackDisplayControl",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Called by WindowResizeHandler when a resize operation completes
        /// </summary>
        public void OnResizeCompleted()
        {
            try
            {
                if (!_isResizeInProgress) return;

                _isResizeInProgress = false;
                _layoutUpdatesPaused = false;

                // Force a layout update to ensure everything is properly arranged
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        InvalidateArrange();
                        InvalidateMeasure();

                        // Restore scroll position after layout is complete
                        Dispatcher.UIThread.Post(() =>
                        {
                            RestoreScrollPosition();

                            // Resume visibility checks
                            CheckVisibleItems();
                        }, DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error in deferred layout update",
                            ex.Message,
                            ex,
                            false);
                    }
                }, DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling resize completion in TrackDisplayControl",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Gets the index of the first visible item in the viewport with improved accuracy
        /// </summary>
        private int GetFirstVisibleItemIndex()
        {
            if (_scrollViewer == null || ItemsSource == null) return -1;

            try
            {
                var viewportTop = _scrollViewer.Offset.Y;
                var viewportWidth = _scrollViewer.Viewport.Width;
                var (itemHeight, itemWidth, itemsPerRow) = GetItemDimensions(viewportWidth);

                if (itemHeight <= 0 || itemsPerRow <= 0) return -1;

                int firstVisibleIndex;
                if (ViewType == ViewType.List)
                {
                    // For list view, it's straightforward
                    firstVisibleIndex = Math.Max(0, (int)(viewportTop / itemHeight));
                }
                else
                {
                    // For wrap layouts, calculate based on rows
                    var rowIndex = Math.Max(0, (int)(viewportTop / itemHeight));
                    firstVisibleIndex = rowIndex * itemsPerRow;
                }

                // Ensure we don't exceed the actual item count
                var totalItems = GetTotalItemCount();
                if (totalItems > 0)
                {
                    firstVisibleIndex = Math.Min(firstVisibleIndex, totalItems - 1);
                }

                return firstVisibleIndex;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets the total number of items in the current ItemsSource
        /// </summary>
        private int GetTotalItemCount()
        {
            if (ItemsSource == null) return 0;

            try
            {
                if (ItemsSource is IList<TrackDisplayModel> list)
                {
                    return list.Count;
                }
                else if (ItemsSource is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
                else if (ItemsSource is System.Collections.IEnumerable enumerable)
                {
                    return enumerable.Cast<object>().Count();
                }
            }
            catch
            {
                // Ignore errors and return 0
            }

            return 0;
        }

        /// <summary>
        /// Restores scroll position after resize, with fallback strategy
        /// </summary>
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
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error restoring scroll position",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void DetachEventHandlers()
        {
            // Stop the timer
            _visibilityCheckTimer?.Stop();
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }

            if (_itemsRepeater != null)
            {
                _itemsRepeater.RemoveHandler(InputElement.PointerReleasedEvent, ArtistClicked);
                _itemsRepeater.RemoveHandler(InputElement.PointerEnteredEvent, Track_PointerEntered);
                _itemsRepeater.RemoveHandler(InputElement.PointerExitedEvent, Track_PointerExited);
                _itemsRepeater.RemoveHandler(DragDrop.DragOverEvent, Track_DragOver);
                _itemsRepeater.RemoveHandler(DragDrop.DropEvent, Track_Drop);
                _itemsRepeater.RemoveHandler(InputElement.PointerPressedEvent, Track_PointerPressed);
                _itemsRepeater.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;
            _isAutoScrolling = false;

            _messenger?.Unregister<ScrollToTrackMessage>(this);

            // Unregister from WindowResizeHandler
            _windowResizeHandler?.UnregisterTrackDisplayControl(this);

            // Stop the timer
            _visibilityCheckTimer?.Stop();
            _visibilityCheckTimer = null;

            // Detach event handlers
            DetachEventHandlers();

            // Clear references
            _scrollViewer = null;
            _itemsRepeater = null;
            _windowResizeHandler = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}