using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Services;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.LibraryEnums;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public abstract partial class SortableCollectionViewModel : ViewModelBase
    {
        protected readonly TrackSortService _trackSortService;
        protected readonly IMessenger _messenger;
        protected readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private SortType _currentSortType = SortType.Name;

        [ObservableProperty]
        private SortDirection _currentSortDirection = SortDirection.Ascending;

        protected SortableCollectionViewModel(
            TrackSortService trackSortService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _trackSortService = trackSortService;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            _messenger.Register<SortUpdateMessage>(this, (r, m) => HandleSortMessage(m));
        }

        private void HandleSortMessage(SortUpdateMessage message)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    OnSortSettingsReceived(message.SortType, message.SortDirection, message.IsUserInitiated);
                },
                $"Processing sort update ({message.SortType}, {message.SortDirection})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public virtual void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Update internal state
                    CurrentSortType = sortType;
                    CurrentSortDirection = direction;

                    // Only apply sort if this is a user-initiated change
                    // or if the derived class specifically requests it
                    if (isUserInitiated)
                    {
                        ApplyCurrentSort();
                    }
                },
                $"Applying sort settings ({sortType}, {direction})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        // Each derived class must implement its own sorting logic
        protected abstract void ApplyCurrentSort();
    }
}