using Avalonia.Controls;
using OmegaMusicPlayer.Core;

namespace OmegaMusicPlayer.Features.Search.Views
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