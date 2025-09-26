using Avalonia.Controls;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Shell.ViewModels;

namespace OmegaMusicPlayer.Features.Shell.Views
{
    public partial class TrackPropertiesDialog : Window
    {
        public TrackPropertiesDialog()
        {
            InitializeComponent();
        }

        public void Initialize(TrackDisplayModel track)
        {
            DataContext = new TrackPropertiesDialogViewModel(this, track);
        }
    }
}