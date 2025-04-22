using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

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
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackSortService(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public IEnumerable<TrackDisplayModel> SortTracks(
            IEnumerable<TrackDisplayModel> tracks,
            SortType sortType,
            SortDirection direction)
        {
            return _errorHandlingService.SafeExecute(
                () =>
                {
                    // Handle null or empty collection
                    if (tracks == null || !tracks.Any())
                    {
                        return Enumerable.Empty<TrackDisplayModel>();
                    }

                    // Apply the appropriate sort
                    var sortedTracks = sortType switch
                    {
                        SortType.Name => tracks.OrderBy(t => t?.Title ?? string.Empty),
                        SortType.Artist => tracks.OrderBy(t => t?.Artists?.FirstOrDefault()?.ArtistName ?? string.Empty),
                        SortType.Album => tracks.OrderBy(t => t?.AlbumTitle ?? string.Empty),
                        SortType.Duration => tracks.OrderBy(t => t?.Duration ?? TimeSpan.Zero),
                        SortType.Genre => tracks.OrderBy(t => t?.Genre ?? string.Empty),
                        SortType.ReleaseDate => tracks.OrderBy(t => t?.ReleaseDate ?? DateTime.MinValue),
                        _ => tracks.OrderBy(t => t?.Title ?? string.Empty)
                    };

                    // Apply direction
                    return direction == SortDirection.Descending ?
                           sortedTracks.Reverse() :
                           sortedTracks;
                },
                $"Sorting tracks by {sortType} in {direction} direction",
                tracks ?? Enumerable.Empty<TrackDisplayModel>(), // Original collection as fallback
                ErrorSeverity.NonCritical,
                false
            );
        }
        public IEnumerable<T> SortItems<T>(
            IEnumerable<T> items,
            SortType sortType,
            SortDirection direction,
            Func<T, string> nameSelector,
            Func<T, int> numberSelector = null)
        {
            return _errorHandlingService.SafeExecute(
                () =>
                {
                    // Handle null or empty collection
                    if (items == null || !items.Any())
                    {
                        return Enumerable.Empty<T>();
                    }

                    // Handle null selectors
                    if (nameSelector == null && (sortType != SortType.Duration || numberSelector == null))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid sort selectors",
                            "Sorting was attempted with null selector functions",
                            null,
                            false);
                        return items; // Return original collection
                    }

                    // Safe version of name selector to handle nulls
                    Func<T, string> safeNameSelector = item =>
                    {
                        try
                        {
                            return nameSelector(item) ?? string.Empty;
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    };

                    // Safe version of number selector to handle exceptions
                    Func<T, int> safeNumberSelector = null;
                    if (numberSelector != null)
                    {
                        safeNumberSelector = item =>
                        {
                            try
                            {
                                return numberSelector(item);
                            }
                            catch
                            {
                                return 0;
                            }
                        };
                    }

                    // Apply the appropriate sort
                    var sortedItems = sortType switch
                    {
                        SortType.Name => items.OrderBy(safeNameSelector),
                        SortType.Duration when safeNumberSelector != null => items.OrderBy(safeNumberSelector),
                        _ => items.OrderBy(safeNameSelector)
                    };

                    // Apply direction
                    return direction == SortDirection.Descending ?
                           sortedItems.Reverse() :
                           sortedItems;
                },
                $"Sorting generic items by {sortType} in {direction} direction",
                items ?? Enumerable.Empty<T>(),
                ErrorSeverity.NonCritical,
                false
            );
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