using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Services;
using System.Collections.ObjectModel;
using System.Linq;
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
            _messenger.Register<SortUpdateMessage>(this, (r, m) =>
            {
                CurrentSortType = m.SortType;
                CurrentSortDirection = m.SortDirection;
                ApplyCurrentSort();
            });
        }

        // Each derived class must implement its own sorting logic
        protected abstract void ApplyCurrentSort();
    }
}