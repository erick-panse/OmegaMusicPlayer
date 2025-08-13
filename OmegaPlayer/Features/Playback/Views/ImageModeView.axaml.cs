using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Playback.Views
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