using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.UI.Controls.TrackDisplay
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

        private ScrollViewer _scrollViewer;
        private ItemsRepeater _itemsRepeater;
        private const double AutoScrollThreshold = 50.0; // Pixels from edge to trigger auto-scroll
        private const double AutoScrollSpeed = 15.0; // Pixels per frame
        private bool _isAutoScrolling;
        private Point _lastDragPosition;
        private bool _isDisposed = false;
        private IErrorHandlingService _errorHandlingService;
        private bool _autoScrollTaskRunning = false;
        private DispatcherTimer _visibilityCheckTimer;

        public TrackDisplayControl()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();

            InitializeComponent();

            // Initialize timer for batched visibility checks (performance optimization)
            _visibilityCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Check every 100ms instead of on every scroll event
            };
            _visibilityCheckTimer.Tick += (s, e) =>
            {
                _visibilityCheckTimer.Stop();
                CheckVisibleItems();
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

                // Initial check for visible items
                Dispatcher.UIThread.Post(() => CheckVisibleItems(), DispatcherPriority.Background);
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
            _scrollViewer = sender as ScrollViewer;

            // Use timer-based batching to reduce excessive calls during fast scrolling
            _visibilityCheckTimer.Stop();
            _visibilityCheckTimer.Start();
        }

        private async void CheckVisibleItems()
        {
            if (_isDisposed || _scrollViewer == null || ItemsSource == null) return;

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
            return ViewType switch
            {
                ViewType.List => (55, viewportWidth, 1), // List: single column, 55px height
                ViewType.Card => (210, 151, Math.Max(1, (int)(viewportWidth / 151))), // Card: 151px width, 210px height
                ViewType.Image => (145, 145, Math.Max(1, (int)(viewportWidth / 145))), // Image: 145px square
                ViewType.RoundImage => (170, 139, Math.Max(1, (int)(viewportWidth / 139))), // Round: 139px width, 170px height
                _ => (55, viewportWidth, 1)
            };
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

        // Replace the index finding logic in Track_DragOver method

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

            // Stop the timer
            _visibilityCheckTimer?.Stop();
            _visibilityCheckTimer = null;

            // Detach event handlers
            DetachEventHandlers();

            // Clear references
            _scrollViewer = null;
            _itemsRepeater = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}