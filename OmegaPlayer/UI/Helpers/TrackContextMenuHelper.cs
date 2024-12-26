using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using System.Windows.Input;

namespace OmegaPlayer.UI.Controls.Helpers
{
    public class TrackContextMenuHelper : AvaloniaObject
    {
        public static readonly AttachedProperty<ICommand> PlayCommandProperty =
            AvaloniaProperty.RegisterAttached<TrackContextMenuHelper, Border, ICommand>("PlayCommand");

        public static readonly AttachedProperty<ICommand> AddToQueueCommandProperty =
            AvaloniaProperty.RegisterAttached<TrackContextMenuHelper, Border, ICommand>("AddToQueueCommand");

        public static readonly AttachedProperty<ICommand> AddAsNextCommandProperty =
            AvaloniaProperty.RegisterAttached<TrackContextMenuHelper, Border, ICommand>("AddAsNextCommand");

        public static readonly AttachedProperty<ICommand> OpenAlbumCommandProperty =
            AvaloniaProperty.RegisterAttached<TrackContextMenuHelper, Border, ICommand>("OpenAlbumCommand");

        public static void SetPlayCommand(Border element, ICommand value)
            => element.SetValue(PlayCommandProperty, value);

        public static ICommand GetPlayCommand(Border element)
            => element.GetValue(PlayCommandProperty);

        public static void SetAddToQueueCommand(Border element, ICommand value)
            => element.SetValue(AddToQueueCommandProperty, value);

        public static ICommand GetAddToQueueCommand(Border element)
            => element.GetValue(AddToQueueCommandProperty);

        public static void SetAddAsNextCommand(Border element, ICommand value)
            => element.SetValue(AddAsNextCommandProperty, value);

        public static ICommand GetAddAsNextCommand(Border element)
            => element.GetValue(AddAsNextCommandProperty);

        public static void SetOpenAlbumCommand(Border element, ICommand value)
            => element.SetValue(OpenAlbumCommandProperty, value);

        public static ICommand GetOpenAlbumCommand(Border element)
            => element.GetValue(OpenAlbumCommandProperty);

        public static void AttachContextMenu(Border border, LibraryViewModel viewModel)
        {
            var contextMenu = new ContextMenu();

            var playMenuItem = new MenuItem { Header = "Play" };
            playMenuItem.Command = viewModel.PlayTrackCommand;
            playMenuItem.CommandParameter = border.DataContext;
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addToQueueMenuItem = new MenuItem { Header = "Add to Queue" };
            addToQueueMenuItem.Command = viewModel.AddToQueueCommand;
            addToQueueMenuItem.CommandParameter = border.DataContext;
            addToQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addAsNextMenuItem = new MenuItem { Header = "Add as Next" };
            addAsNextMenuItem.Command = viewModel.AddAsNextTracksCommand;
            addAsNextMenuItem.CommandParameter = border.DataContext;
            addAsNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var separator = new Separator();

            var openAlbumMenuItem = new MenuItem { Header = "Go to Album" };
            openAlbumMenuItem.Command = viewModel.OpenAlbumCommand;
            openAlbumMenuItem.CommandParameter = (border.DataContext as TrackDisplayModel)?.AlbumID;

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addToQueueMenuItem);
            contextMenu.Items.Add(addAsNextMenuItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(openAlbumMenuItem);

            border.ContextMenu = contextMenu;
        }
    }
}