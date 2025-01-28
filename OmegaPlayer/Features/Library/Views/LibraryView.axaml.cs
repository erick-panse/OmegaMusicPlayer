using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}