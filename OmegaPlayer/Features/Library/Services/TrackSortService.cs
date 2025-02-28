using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Linq;

namespace OmegaPlayer.Features.Library.Services
{
    public enum SortType
    {
        Name,
        Artist,
        Album,
        Duration,
        Genre,
        ReleaseDate,
        TrackCount
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public class TrackSortService
    {
        public IEnumerable<TrackDisplayModel> SortTracks(
            IEnumerable<TrackDisplayModel> tracks,
            SortType sortType,
            SortDirection direction)
        {
            var sortedTracks = sortType switch
            {
                SortType.Name => tracks.OrderBy(t => t.Title),
                SortType.Artist => tracks.OrderBy(t => t.Artists.FirstOrDefault()?.ArtistName ?? string.Empty),
                SortType.Album => tracks.OrderBy(t => t.AlbumTitle),
                SortType.Duration => tracks.OrderBy(t => t.Duration),
                SortType.Genre => tracks.OrderBy(t => t.Genre),
                SortType.ReleaseDate => tracks.OrderBy(t => t.ReleaseDate),
                _ => tracks.OrderBy(t => t.Title)
            };

            return direction == SortDirection.Descending ?
                   sortedTracks.Reverse() :
                   sortedTracks;
        }

        public IEnumerable<T> SortItems<T>(
            IEnumerable<T> items,
            SortType sortType,
            SortDirection direction,
            Func<T, string> nameSelector,
            Func<T, int> numberSelector = null)
        {
            var sortedItems = sortType switch
            {
                SortType.Name => items.OrderBy(nameSelector),
                SortType.Duration when numberSelector != null => items.OrderBy(numberSelector),
                _ => items.OrderBy(nameSelector)
            };

            return direction == SortDirection.Descending ?
                   sortedItems.Reverse() :
                   sortedItems;
        }
    }

    public class SortUpdateMessage
    {
        public SortType SortType { get; }
        public SortDirection SortDirection { get; }
        public bool IsUserInitiated { get; }

        public SortUpdateMessage(SortType sortType, SortDirection sortDirection, bool isUserInitiated = false)
        {
            SortType = sortType;
            SortDirection = sortDirection;
            IsUserInitiated = isUserInitiated;
        }
    }

}