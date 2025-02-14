using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Search.Views
{

    public partial class SearchView : UserControl
    {
        public SearchView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}