using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Infrastructure.Services;
using System;

namespace OmegaPlayer.Features.Playback.ViewModels
{
    public partial class SleepTimerDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly SleepTimerManager _timerManager;

        [ObservableProperty]
        private int _minutes;

        [ObservableProperty]
        private bool _finishLastSong;

        public bool IsTimerRunning => _timerManager.IsTimerActive;
        public string RemainingTimeText => _timerManager.RemainingTime;

        public SleepTimerDialogViewModel(Window dialog)
        {
            _dialog = dialog;
            _timerManager = SleepTimerManager.Instance;

            _timerManager.CheckAndUpdateTimerState();

            InitializeTimerState();

            // Subscribe to timer manager property changes
            _timerManager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SleepTimerManager.RemainingTime))
                {
                    OnPropertyChanged(nameof(RemainingTimeText));
                }
                else if (e.PropertyName == nameof(SleepTimerManager.IsTimerActive))
                {
                    OnPropertyChanged(nameof(IsTimerRunning));
                }
            };
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
            _timerManager.StartTimer(Minutes, FinishLastSong);
            _dialog.Close((Minutes, FinishLastSong));
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