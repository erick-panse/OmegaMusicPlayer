using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public abstract partial class SortableCollectionViewModel : ViewModelBase
    {
        protected readonly TrackSortService _trackSortService;
        protected readonly IMessenger _messenger;

        [ObservableProperty]
        private SortType _currentSortType = SortType.Name;

        [ObservableProperty]
        private SortDirection _currentSortDirection = SortDirection.Ascending;

        protected SortableCollectionViewModel(TrackSortService trackSortService, IMessenger messenger)
        {
            _trackSortService = trackSortService;
            _messenger = messenger;

            // Register for sort updates from MainViewModel
            _messenger.Register<SortUpdateMessage>(this, (r, m) => OnSortSettingsReceived(m.SortType, m.SortDirection, m.IsUserInitiated));

        }

        public virtual void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
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
        }


        // Each derived class must implement its own sorting logic
        protected abstract void ApplyCurrentSort();
    }
}