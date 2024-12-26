using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using System.Diagnostics;
using Avalonia.Media;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class AlbumView : UserControl
    {
        private ItemsControl _itemsControl;

        public AlbumView()
        {
            InitializeComponent();

            // Find the ItemsControl by name
            _itemsControl = this.Find<ItemsControl>("PART_ItemsControl");
            if (_itemsControl != null)
            {
                _itemsControl.LayoutUpdated += ItemsControl_LayoutUpdated;
            }
        }

        private void ItemsControl_LayoutUpdated(object sender, System.EventArgs e)
        {
            if (_itemsControl == null) return;

            var buttons = _itemsControl.GetVisualDescendants()
                .OfType<Button>()
                .Where(b => b.Name == "AlbumButton" && b.ContextMenu == null);

            foreach (var button in buttons)
            {
                AttachContextMenu(button);
            }
        }

        private void AttachContextMenu(Button button)
        {
            var viewModel = this.DataContext as AlbumViewModel;
            if (viewModel == null)
            {
                Debug.WriteLine("ViewModel is null");
                return;
            }

            var album = button.DataContext as AlbumDisplayModel;
            if (album == null)
            {
                Debug.WriteLine("Album is null");
                return;
            }

            var contextMenu = new ContextMenu
            {
                DataContext = album
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play Album",
                Command = viewModel.PlayAlbumTracksCommand,
                CommandParameter = album
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addNextMenuItem = new MenuItem
            {
                Header = "Add to Next",
                Command = viewModel.AddAlbumTracksToNextCommand,
                CommandParameter = album
            };
            addNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddAlbumTracksToQueueCommand,
                CommandParameter = album
            };
            addQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addNextMenuItem);
            contextMenu.Items.Add(addQueueMenuItem);

            button.ContextMenu = contextMenu;
            Debug.WriteLine($"Attached context menu to album: {album.Title}");
        }
    }
}