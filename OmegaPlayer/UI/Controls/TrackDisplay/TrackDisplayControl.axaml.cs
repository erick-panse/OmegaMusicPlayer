using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

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
        private ItemsControl _itemsControl;
        private const double AutoScrollThreshold = 50.0; // Pixels from edge to trigger auto-scroll
        private const double AutoScrollSpeed = 15.0; // Pixels per frame
        private bool _isAutoScrolling;
        private Point _lastDragPosition;
        private bool _isDisposed = false;
        private IErrorHandlingService _errorHandlingService;
        private bool _autoScrollTaskRunning = false;

        public TrackDisplayControl()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();

            InitializeComponent();
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
                if (current is ScrollViewer sv && sv.Name == "DetailsScrollViewer")
                {
                    _scrollViewer = sv;
                    break;
                }
                current = current.GetVisualParent();
            }

            if (_scrollViewer == null)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "ScrollViewer not found",
                    "DetailsScrollViewer not found in visual tree for TrackDisplayControl",
                    null,
                    false);
            }

            // Find the items control and attach event handlers
            _itemsControl = e.NameScope.Find<ItemsControl>("TracksItemsControl");
            if (_itemsControl != null)
            {
                _itemsControl.AddHandler(InputElement.PointerReleasedEvent, ArtistClicked, RoutingStrategies.Tunnel);
                _itemsControl.AddHandler(InputElement.PointerEnteredEvent, Track_PointerEntered, RoutingStrategies.Tunnel);
                _itemsControl.AddHandler(InputElement.PointerExitedEvent, Track_PointerExited, RoutingStrategies.Tunnel);

                // Add drag-drop handlers at the container level
                _itemsControl.AddHandler(DragDrop.DragOverEvent, Track_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                _itemsControl.AddHandler(DragDrop.DropEvent, Track_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                _itemsControl.AddHandler(InputElement.PointerPressedEvent, Track_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

                // Enable drag-drop on the ItemsControl itself
                DragDrop.SetAllowDrop(_itemsControl, true);

                // Add pointer wheel handler for scrolling while dragging
                _itemsControl.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            }
            else
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "ItemsControl not found",
                    "TracksItemsControl not found in template for TrackDisplayControl",
                    null,
                    false);
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

                // Store current drag position
                _lastDragPosition = e.GetPosition(_scrollViewer);

                // Calculate distances from edges
                double distanceFromTop = _lastDragPosition.Y;
                double distanceFromBottom = _scrollViewer.Bounds.Height - _lastDragPosition.Y;

                // Determine if auto-scroll should start
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

                // Find the track container - look for either container types
                var element = e.Source as Visual;
                while (element != null &&
                       !(element is Border border &&
                         (border.Name == "TrackContainer" || border.Name == "TrackOuterContainer")))
                {
                    element = element.GetVisualParent();
                }

                if (element is Border container)
                {
                    // Get the actual track data, which might be in the parent's DataContext
                    var track = container.DataContext as TrackDisplayModel;
                    if (track == null && container.Parent != null)
                    {
                        track = (container.Parent as StyledElement)?.DataContext as TrackDisplayModel;
                    }

                    if (track != null)
                    {
                        var itemsControl = sender as ItemsControl;
                        if (itemsControl != null)
                        {
                            e.DragEffects = DragDropEffects.Move;
                            int index = itemsControl.Items.IndexOf(track);
                            viewModel.HandleTrackDragOver(index);
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling track drag over in TrackDisplayControl",
                    ex.Message,
                    ex,
                    false);

                // Stop auto-scrolling on error
                _isAutoScrolling = false;
            }
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
            if (_itemsControl != null)
            {
                _itemsControl.RemoveHandler(InputElement.PointerReleasedEvent, ArtistClicked);
                _itemsControl.RemoveHandler(InputElement.PointerEnteredEvent, Track_PointerEntered);
                _itemsControl.RemoveHandler(InputElement.PointerExitedEvent, Track_PointerExited);
                _itemsControl.RemoveHandler(DragDrop.DragOverEvent, Track_DragOver);
                _itemsControl.RemoveHandler(DragDrop.DropEvent, Track_Drop);
                _itemsControl.RemoveHandler(InputElement.PointerPressedEvent, Track_PointerPressed);
                _itemsControl.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;
            _isAutoScrolling = false;

            // Detach event handlers
            DetachEventHandlers();

            // Clear references
            _scrollViewer = null;
            _itemsControl = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}