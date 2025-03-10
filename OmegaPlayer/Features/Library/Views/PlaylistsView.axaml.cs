using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class PlaylistsView : UserControl
    {
        public PlaylistsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}