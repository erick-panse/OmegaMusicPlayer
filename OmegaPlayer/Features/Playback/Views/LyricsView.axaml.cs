using Avalonia.Controls;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Playback.Views
{
    public partial class LyricsView : UserControl
    {
        public LyricsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}