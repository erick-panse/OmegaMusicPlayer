using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class FoldersView : UserControl
    {
        public FoldersView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}