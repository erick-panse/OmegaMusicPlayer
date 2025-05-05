using System;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.UI;

namespace OmegaPlayer.Infrastructure.Services
{
    public partial class SleepTimerManager : ObservableObject, IDisposable
    {
        private readonly IErrorHandlingService _errorHandlingService;
        private static readonly object _lockObject = new object();

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

        // Property to access the singleton, creating it on-demand with error handling service
        public static SleepTimerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            // Retrieve the error handling service from the service provider
                            var errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
                            if (errorHandlingService == null)
                            {
                                throw new InvalidOperationException(
                                    "ErrorHandlingService is not registered in the service provider. " +
                                    "Make sure it is properly registered in App.xaml.cs");
                            }

                            _instance = new SleepTimerManager(errorHandlingService);
                        }
                    }
                }
                return _instance;
            }
        }

        // Private constructor that requires errorHandlingService
        private SleepTimerManager(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;

            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    _updateTimer?.Dispose();
                    _updateTimer = new Timer(1000);
                    _updateTimer.Elapsed += UpdateTimer_Elapsed;
                },
                "Initializing sleep timer",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public void StartTimer(int minutes, bool finishLastSong)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    InitializeTimer();

                    EndTime = DateTime.Now.AddMinutes(minutes);
                    FinishLastSong = finishLastSong;
                    IsTimerActive = true;
                    TimerExpiredNaturally = false;

                    UpdateRemainingTime();
                    _updateTimer.Start();
                },
                $"Starting sleep timer for {minutes} minutes",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public void StopTimer()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    _updateTimer.Stop();
                    IsTimerActive = false;
                    EndTime = null;
                    RemainingTime = "";
                    FinishLastSong = false;
                    TimerExpiredNaturally = false;
                },
                "Stopping sleep timer",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () => UpdateRemainingTime(),
                "Updating timer remaining time",
                ErrorSeverity.NonCritical,
                false
            );
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

                _errorHandlingService.SafeExecute(
                    () =>
                    {
                        if (Dispatcher.UIThread.CheckAccess())
                        {
                            RemainingTime = $"{minutes}:{seconds:00}";
                        }
                        else
                        {
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                RemainingTime = $"{minutes}:{seconds:00}";
                            });
                        }
                    },
                    "Updating timer display",
                    ErrorSeverity.NonCritical,
                    false
                );
            }
            else
            {
                TimerExpiredNaturally = true;
                StopTimer();
            }
        }

        public void CheckAndUpdateTimerState()
        {
            _errorHandlingService.SafeExecute(
                () =>
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
                },
                "Checking and updating timer state",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public void Dispose()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    _updateTimer?.Dispose();
                    _updateTimer = null;
                },
                "Disposing sleep timer",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}