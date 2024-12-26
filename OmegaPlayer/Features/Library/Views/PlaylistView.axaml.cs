using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using System.Diagnostics;
using Avalonia.Media;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class PlaylistView : UserControl
    {
        private ItemsControl _itemsControl;

        public PlaylistView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

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
                .Where(b => b.Name == "PlaylistButton" && b.ContextMenu == null);

            foreach (var button in buttons)
            {
                AttachContextMenu(button);
            }
        }

        private void AttachContextMenu(Button button)
        {
            var viewModel = this.DataContext as PlaylistViewModel;
            if (viewModel == null)
            {
                Debug.WriteLine("ViewModel is null");
                return;
            }

            var playlist = button.DataContext as PlaylistDisplayModel;
            if (playlist == null)
            {
                Debug.WriteLine("Playlist is null");
                return;
            }

            var contextMenu = new ContextMenu
            {
                DataContext = playlist
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play Playlist",
                Command = viewModel.PlayPlaylistTracksCommand,
                CommandParameter = playlist
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addNextMenuItem = new MenuItem
            {
                Header = "Add to Next",
                Command = viewModel.AddPlaylistTracksToNextCommand,
                CommandParameter = playlist
            };
            addNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddPlaylistTracksToQueueCommand,
                CommandParameter = playlist
            };
            addQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addNextMenuItem);
            contextMenu.Items.Add(addQueueMenuItem);

            button.ContextMenu = contextMenu;
            Debug.WriteLine($"Attached context menu to playlist: {playlist.Title}");
        }
    }
}