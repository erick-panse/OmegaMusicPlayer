using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Media;

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

        private ItemsControl _itemsControl;

        public TrackDisplayControl()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            if (e.NameScope.Find<ItemsControl>("PART_ItemsControl") is ItemsControl itemsControl)
            {
                _itemsControl = itemsControl;
                itemsControl.AddHandler(InputElement.PointerReleasedEvent, ArtistClicked, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerEnteredEvent, Track_PointerEntered, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerExitedEvent, Track_PointerExited, RoutingStrategies.Tunnel);

                // Subscribe to ItemsControl's LayoutUpdated event
                itemsControl.LayoutUpdated += ItemsControl_LayoutUpdated;
            }
        }

        private void ItemsControl_LayoutUpdated(object sender, System.EventArgs e)
        {
            var borders = _itemsControl.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Classes.Contains("trackPanel") && b.ContextMenu == null);

            foreach (var border in borders)
            {
                AttachContextMenu(border);
            }
        }

        private void AttachContextMenu(Border border)
        {
            var viewModel = this.DataContext as LibraryViewModel;
            if (viewModel == null) return;

            var track = border.DataContext as TrackDisplayModel;
            if (track == null) return;

            var contextMenu = new ContextMenu
            {
                DataContext = track // Set the track as the DataContext for the context menu
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play",
                Command = viewModel.PlayTrackCommand,
                CommandParameter = track
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addToQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddToQueueCommand,
                CommandParameter = track
            };
            addToQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addAsNextMenuItem = new MenuItem
            {
                Header = "Add as Next",
                Command = viewModel.AddAsNextTracksCommand,
                CommandParameter = track
            };
            addAsNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var openAlbumMenuItem = new MenuItem
            {
                Header = "Go to Album",
                Command = viewModel.OpenAlbumCommand,
                CommandParameter = track.AlbumID
            };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addToQueueMenuItem);
            contextMenu.Items.Add(addAsNextMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(openAlbumMenuItem);

            border.ContextMenu = contextMenu;
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