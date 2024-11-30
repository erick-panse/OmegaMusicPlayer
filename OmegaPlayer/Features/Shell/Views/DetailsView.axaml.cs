using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.ViewModels;
using Avalonia;
using OmegaPlayer.Features.Shell.ViewModels;

namespace OmegaPlayer.Features.Shell.Views
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