using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
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
        private const double AutoScrollThreshold = 50.0; // Pixels from edge to trigger auto-scroll
        private const double AutoScrollSpeed = 15.0; // Pixels per frame
        private bool _isAutoScrolling;
        private Point _lastDragPosition;

        public TrackDisplayControl()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Get reference to ScrollViewer MainScrollViewer - Do not change
            Visual current = this;
            while (current != null)
            {
                if (current is ScrollViewer sv && sv.Name == "MainScrollViewer")
                {
                    _scrollViewer = sv;
                    break;
                }
                current = current.GetVisualParent();
            }

            if (_scrollViewer == null)
            {
                Console.WriteLine("Warning: MainScrollViewer not found in visual tree");
            }

            if (e.NameScope.Find<ItemsControl>("PART_ItemsControl") is ItemsControl itemsControl)
            {
                itemsControl.AddHandler(InputElement.PointerReleasedEvent, ArtistClicked, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerEnteredEvent, Track_PointerEntered, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerExitedEvent, Track_PointerExited, RoutingStrategies.Tunnel);

                // Add drag-drop handlers at the container leve
                itemsControl.AddHandler(DragDrop.DragOverEvent, Track_DragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                itemsControl.AddHandler(DragDrop.DropEvent, Track_Drop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
                itemsControl.AddHandler(InputElement.PointerPressedEvent, Track_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

                // Enable drag-drop on the ItemsControl itself
                DragDrop.SetAllowDrop(itemsControl, true);

                // Add pointer wheel handler for scrolling while dragging
                itemsControl.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            }
        }
        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (_scrollViewer != null && DataContext is DetailsViewModel viewModel && viewModel.DraggedTrack != null)
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
            while (_isAutoScrolling)
            {
                _scrollViewer.Offset = new Vector(
                    _scrollViewer.Offset.X,
                    Math.Clamp(_scrollViewer.Offset.Y + scrollAmount, 0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height)
                );
                await Task.Delay(16); // Approximately 60 FPS
            }
        }

        private async void Track_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
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
            if (_scrollViewer == null || !(DataContext is DetailsViewModel viewModel)) return;

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

        private void Track_Drop(object? sender, DragEventArgs e)
        {
            _isAutoScrolling = false;
            if (DataContext is DetailsViewModel viewModel)
            {
                viewModel.HandleTrackDrop();
                e.Handled = true;
            }
        }

        private void ArtistClicked(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Source is TextBlock textBlock &&
                textBlock.Name == "ArtistTextBlock" &&
                textBlock.Tag is Artists artist &&
                DataContext is LibraryViewModel viewModel)
            {
                viewModel.OpenArtistCommand.Execute(artist);
                e.Handled = true;
            }
        }

        private void Track_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = true;
            }
        }

        private void Track_PointerExited(object? sender, PointerEventArgs e)
        {
            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = false;
            }
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("Debug", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}