using Avalonia.Controls;
using OmegaMusicPlayer.Core;

namespace OmegaMusicPlayer.Features.Library.Views
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