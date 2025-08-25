using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.ComponentModel;

namespace OmegaPlayer.Features.Playback.ViewModels
{
    public partial class SleepTimerDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly SleepTimerManager _timerManager; 
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private int _minutes;

        [ObservableProperty]
        private bool _finishLastSong;

        public bool IsTimerRunning => _timerManager.IsTimerActive;
        public string RemainingTimeText => _timerManager.RemainingTime;

        public SleepTimerDialogViewModel(Window dialog, LocalizationService localizationService, IErrorHandlingService errorHandlingService)
        {
            _dialog = dialog;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;

            _timerManager = SleepTimerManager.Instance;
            _timerManager.CheckAndUpdateTimerState();

            InitializeTimerState();

            // Subscribe to timer manager property changes
            _timerManager.PropertyChanged += TimerManager_PropertyChanged;
        }

        private void TimerManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SleepTimerManager.RemainingTime))
            {
                OnPropertyChanged(nameof(RemainingTimeText));
            }
            else if (e.PropertyName == nameof(SleepTimerManager.IsTimerActive))
            {
                OnPropertyChanged(nameof(IsTimerRunning));
            }
        }

        private void InitializeTimerState()
        {
            if (_timerManager.IsTimerActive && _timerManager.EndTime.HasValue)
            {
                var remainingTime = _timerManager.EndTime.Value - DateTime.Now;
                Minutes = (int)Math.Ceiling(remainingTime.TotalMinutes);
                FinishLastSong = _timerManager.FinishLastSong;
            }
            else
            {
                // Default value
                Minutes = 1;
                FinishLastSong = true;
            }
        }

        [RelayCommand]
        private void Start()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Validate to ensure minutes in a reasonable range
                    if (Minutes <= 0 || Minutes > 1440) // 24 hours max
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid sleep timer duration",
                            $"Sleep timer duration must be between 1 and 1440 minutes, got {Minutes}",
                            null,
                            false);
                        return;
                    }

                    _timerManager.StartTimer(Minutes, FinishLastSong);
                    _dialog.Close((Minutes, FinishLastSong));
                },
                _localizationService["ErrorStartingSleepTimer"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        private void Stop()
        {
            _timerManager.StopTimer();
            _dialog.Close((-1, false));
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }
    }
}