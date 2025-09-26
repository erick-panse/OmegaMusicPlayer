using Avalonia.Controls;
using OmegaMusicPlayer.Core;

namespace OmegaMusicPlayer.Features.Playback.Views
{
    public partial class ImageModeView : UserControl
    {
        public ImageModeView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}