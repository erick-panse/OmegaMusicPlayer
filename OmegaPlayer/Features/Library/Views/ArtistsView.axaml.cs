using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{

    public partial class ArtistsView : UserControl
    {
        public ArtistsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}