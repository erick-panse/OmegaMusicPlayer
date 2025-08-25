using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Playback.Views
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