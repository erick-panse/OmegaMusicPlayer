using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using System.Diagnostics;
using Avalonia.Media;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class GenreView : UserControl
    {
        private ItemsControl _itemsControl;

        public GenreView()
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
                .Where(b => b.Name == "GenreButton" && b.ContextMenu == null);

            foreach (var button in buttons)
            {
                AttachContextMenu(button);
            }
        }

        private void AttachContextMenu(Button button)
        {
            var viewModel = this.DataContext as GenreViewModel;
            if (viewModel == null)
            {
                Debug.WriteLine("ViewModel is null");
                return;
            }

            var genre = button.DataContext as GenreDisplayModel;
            if (genre == null)
            {
                Debug.WriteLine("Genre is null");
                return;
            }

            var contextMenu = new ContextMenu
            {
                DataContext = genre
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play Genre",
                Command = viewModel.PlayGenreTracksCommand,
                CommandParameter = genre
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addNextMenuItem = new MenuItem
            {
                Header = "Add to Next",
                Command = viewModel.AddGenreTracksToNextCommand,
                CommandParameter = genre
            };
            addNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddGenreTracksToQueueCommand,
                CommandParameter = genre
            };
            addQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addNextMenuItem);
            contextMenu.Items.Add(addQueueMenuItem);

            button.ContextMenu = contextMenu;
            Debug.WriteLine($"Attached context menu to genre: {genre.Name}");
        }
    }
}