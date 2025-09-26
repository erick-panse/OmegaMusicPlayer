using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.UI;

namespace OmegaMusicPlayer.Features.Playback.Views
{
    public partial class SleepTimerDialog : Window
    {
        private IErrorHandlingService _errorHandlingService;
        private LocalizationService _localizationService;

        public SleepTimerDialog()
        {
            InitializeComponent();

            // Get error handling service
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
            _localizationService = App.ServiceProvider.GetService<LocalizationService>();
            var vm = new SleepTimerDialogViewModel(this, _localizationService, _errorHandlingService);
            DataContext = vm;
        }
    }
}