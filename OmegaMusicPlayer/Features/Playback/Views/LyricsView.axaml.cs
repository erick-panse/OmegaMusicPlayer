using Avalonia.Controls;
using OmegaMusicPlayer.Core;

namespace OmegaMusicPlayer.Features.Playback.Views
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