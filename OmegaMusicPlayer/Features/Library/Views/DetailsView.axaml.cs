using Avalonia.Controls;
using OmegaMusicPlayer.Core;

namespace OmegaMusicPlayer.Features.Library.Views
{
    public partial class DetailsView : UserControl
    {
        public DetailsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}