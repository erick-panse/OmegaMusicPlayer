using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OmegaPlayer.Views
{

    public partial class TrackControlView : UserControl
    {
        public TrackControlView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}