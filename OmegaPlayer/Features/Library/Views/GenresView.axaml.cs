using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class GenresView : UserControl
    {
        public GenresView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}