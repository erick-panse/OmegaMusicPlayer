using Avalonia.Controls;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Shell.ViewModels;

namespace OmegaPlayer.Features.Shell.Views
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