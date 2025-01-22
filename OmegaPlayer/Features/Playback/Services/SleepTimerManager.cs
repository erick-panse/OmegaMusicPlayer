using System;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OmegaPlayer.Infrastructure.Services
{
    public partial class SleepTimerManager : ObservableObject
    {
        private static SleepTimerManager _instance;
        private Timer _updateTimer;

        [ObservableProperty]
        private bool _isTimerActive;

        [ObservableProperty]
        private DateTime? _endTime;

        [ObservableProperty]
        private bool _finishLastSong;

        [ObservableProperty]
        private string _remainingTime;

        [ObservableProperty]
        private bool _timerExpiredNaturally;

        public static SleepTimerManager Instance
        {
            get
            {
                _instance ??= new SleepTimerManager();
                return _instance;
            }
        }

        private SleepTimerManager()
        {
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _updateTimer?.Dispose();
            _updateTimer = new Timer(1000);
            _updateTimer.Elapsed += UpdateTimer_Elapsed;
        }

        public void StartTimer(int minutes, bool finishLastSong)
        {
            InitializeTimer();

            EndTime = DateTime.Now.AddMinutes(minutes);
            FinishLastSong = finishLastSong;
            IsTimerActive = true;
            TimerExpiredNaturally = false;

            UpdateRemainingTime();
            _updateTimer.Start();
        }

        public void StopTimer()
        {
            _updateTimer.Stop();
            IsTimerActive = false;
            EndTime = null;
            RemainingTime = "";
            FinishLastSong = false;
            TimerExpiredNaturally = false;
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UpdateRemainingTime();
        }

        private void UpdateRemainingTime()
        {
            if (!IsTimerActive || !EndTime.HasValue)
            {
                StopTimer();
                return;
            }

            var remaining = EndTime.Value - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                var minutes = Math.Floor(remaining.TotalMinutes);
                var seconds = remaining.Seconds;

                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RemainingTime = $"{minutes}:{seconds:00}";
                });
            }
            else
            {
                TimerExpiredNaturally = true;
                StopTimer();
            }
        }
        public void CheckAndUpdateTimerState()
        {
            if (IsTimerActive && EndTime.HasValue)
            {
                var remaining = EndTime.Value - DateTime.Now;
                if (remaining.TotalSeconds > 0)
                {
                    if (!_updateTimer.Enabled)
                    {
                        _updateTimer.Start();
                    }
                    UpdateRemainingTime();
                }
                else
                {
                    StopTimer();
                }
            }
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
        }

    }
}