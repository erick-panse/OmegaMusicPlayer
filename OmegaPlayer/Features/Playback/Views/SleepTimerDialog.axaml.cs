using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Playback.Views
{
    public partial class SleepTimerDialog : Window
    {
        public SleepTimerDialog()
        {
            InitializeComponent();
            var vm = new SleepTimerDialogViewModel(this);
            DataContext = vm;
        }

    }

}