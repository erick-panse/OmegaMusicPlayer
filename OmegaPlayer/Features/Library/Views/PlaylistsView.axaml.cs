using Avalonia.Controls;
using OmegaPlayer.Core;

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