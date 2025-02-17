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

        public TrackDisplayControl()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
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
                DataContext is LibraryViewModel viewModel &&
                viewModel.IsReorderMode)
            {
                // Create drag data
                var data = new DataObject();
                data.Set(DataFormats.Text, track.TrackID.ToString());

                // Start the drag operation
                var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

                if (result == DragDropEffects.Move)
                {
                    viewModel.HandleTrackDragStarted(track);
                }
            }
        }

        private void Track_DragOver(object? sender, DragEventArgs e)
        {
            // Find the track container
            var element = e.Source as Visual;
            while (element != null && !(element is Border border && border.Name == "TrackContainer"))
            {
                element = element.GetVisualParent();
            }

            if (element is Border trackContainer &&
                trackContainer.DataContext is TrackDisplayModel track &&
                DataContext is LibraryViewModel viewModel)
            {
                var itemsControl = sender as ItemsControl;
                if (itemsControl != null)
                {
                    e.DragEffects = DragDropEffects.Move;
                    int index = itemsControl.Items.IndexOf(track);
                    Console.WriteLine($"DragOver index: {index}");
                    viewModel.HandleTrackDragOver(index);
                    e.Handled = true;
                }
            }
        }

        private void Track_Drop(object? sender, DragEventArgs e)
        {
            if (DataContext is LibraryViewModel viewModel)
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