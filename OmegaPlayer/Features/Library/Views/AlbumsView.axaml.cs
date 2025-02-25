using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class AlbumsView : UserControl
    {
        public AlbumsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}