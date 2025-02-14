using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Search.Views
{

    public partial class SearchPreview : UserControl
    {
        public SearchPreview()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}