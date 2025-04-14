using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Playback.Views
{
    public partial class SleepTimerDialog : Window
    {
        private IErrorHandlingService _errorHandlingService;

        public SleepTimerDialog()
        {
            InitializeComponent();

            // Get error handling service
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
            var vm = new SleepTimerDialogViewModel(this, _errorHandlingService);
            DataContext = vm;
        }
    }
}