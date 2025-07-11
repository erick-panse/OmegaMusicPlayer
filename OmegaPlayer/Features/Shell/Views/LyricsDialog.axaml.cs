using Avalonia.Controls;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class LyricsDialog : Window
    {
        public LyricsDialog()
        {
            InitializeComponent();
        }

        public void Initialize(TrackDisplayModel track, LocalizationService localizationService)
        {
            DataContext = new LyricsDialogViewModel(this, track, localizationService);
        }
    }
}