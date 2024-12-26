using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using System.Diagnostics;
using System.Linq;

namespace OmegaPlayer.Features.Library.Views
{

    public partial class ArtistView : UserControl
    {
        private ItemsControl _itemsControl;

        public ArtistView()
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
                .Where(b => b.Name == "ArtistButton" && b.ContextMenu == null);

            foreach (var button in buttons)
            {
                AttachContextMenu(button);
            }
        }

        private void AttachContextMenu(Button button)
        {
            var viewModel = this.DataContext as ArtistViewModel;
            if (viewModel == null)
            {
                Debug.WriteLine("ViewModel is null");
                return;
            }

            var artist = button.DataContext as ArtistDisplayModel;
            if (artist == null)
            {
                Debug.WriteLine("Artist is null");
                return;
            }

            var contextMenu = new ContextMenu
            {
                DataContext = artist
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play Artist",
                Command = viewModel.PlayArtistTracksCommand,
                CommandParameter = artist
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addNextMenuItem = new MenuItem
            {
                Header = "Add to Next",
                Command = viewModel.AddArtistTracksToNextCommand,
                CommandParameter = artist
            };
            addNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddArtistTracksToQueueCommand,
                CommandParameter = artist
            };
            addQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addNextMenuItem);
            contextMenu.Items.Add(addQueueMenuItem);

            button.ContextMenu = contextMenu;
            Debug.WriteLine($"Attached context menu to artist: {artist.Name}");
        }
    }
}