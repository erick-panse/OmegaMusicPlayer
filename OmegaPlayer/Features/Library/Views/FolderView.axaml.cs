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
    public partial class FolderView : UserControl
    {
        private ItemsControl _itemsControl;

        public FolderView()
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
                .Where(b => b.Name == "FolderButton" && b.ContextMenu == null);

            foreach (var button in buttons)
            {
                AttachContextMenu(button);
            }
        }

        private void AttachContextMenu(Button button)
        {
            var viewModel = this.DataContext as FolderViewModel;
            if (viewModel == null)
            {
                Debug.WriteLine("ViewModel is null");
                return;
            }

            var folder = button.DataContext as FolderDisplayModel;
            if (folder == null)
            {
                Debug.WriteLine("Folder is null");
                return;
            }

            var contextMenu = new ContextMenu
            {
                DataContext = folder
            };

            var playMenuItem = new MenuItem
            {
                Header = "Play Folder",
                Command = viewModel.PlayFolderTracksCommand,
                CommandParameter = folder
            };
            playMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("PlayIcon") as Geometry };

            var addNextMenuItem = new MenuItem
            {
                Header = "Add to Next",
                Command = viewModel.AddFolderTracksToNextCommand,
                CommandParameter = folder
            };
            addNextMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            var addQueueMenuItem = new MenuItem
            {
                Header = "Add to Queue",
                Command = viewModel.AddFolderTracksToQueueCommand,
                CommandParameter = folder
            };
            addQueueMenuItem.Icon = new PathIcon { Data = Application.Current.FindResource("AddTrackIcon") as Geometry };

            contextMenu.Items.Add(playMenuItem);
            contextMenu.Items.Add(addNextMenuItem);
            contextMenu.Items.Add(addQueueMenuItem);

            button.ContextMenu = contextMenu;
            Debug.WriteLine($"Attached context menu to folder: {folder.FolderName}");
        }
    }
}