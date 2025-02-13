using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class AllTracksRepository
    {
        public List<TrackDisplayModel> AllTracks { get; private set; } = new();
        public List<Albums> AllAlbums { get; private set; } = new();
        public List<Artists> AllArtists { get; private set; } = new();
        public List<Genres> AllGenres { get; private set; } = new();
        public List<Playlist> AllPlaylists { get; private set; } = new();

        private readonly TrackDisplayRepository _trackDisplayRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistsRepository _artistsRepository;
        private readonly GenresRepository _genresRepository;
        private readonly BlacklistedDirectoryService _blacklistService;
        private readonly ProfileManager _profileManager;

        public AllTracksRepository(
            TrackDisplayRepository trackDisplayRepository,
            AlbumRepository albumRepository,
            ArtistsRepository artistsRepository,
            GenresRepository genresRepository,
            BlacklistedDirectoryService blacklistService,
            ProfileManager profileManager
            )
        {
            _trackDisplayRepository = trackDisplayRepository;
            _albumRepository = albumRepository;
            _artistsRepository = artistsRepository;
            _genresRepository = genresRepository;
            _blacklistService = blacklistService;
            _profileManager = profileManager;

            LoadTracks();
        }

        public async Task LoadTracks()
        {
            await _profileManager.InitializeAsync();
            var currentProfile = _profileManager.CurrentProfile;

            AllTracks = await ValidateBlacklist(currentProfile.ProfileID);

            // AllAlbums, AllArtists and AllGenres use the AllTracks to get the correct items since AllTracks has already filtered tracks in blacklist
            AllAlbums = await GetAlbumsForProfile();
            AllArtists = await GetArtistsForProfile();
            AllGenres = await GetGenresForProfile();

        }
        public async Task<List<TrackDisplayModel>> ValidateBlacklist(int profileId)
        {
            var allTracksToValidate = await _trackDisplayRepository.GetAllTracksWithMetadata(profileId);
            var blacklistedPaths = await _blacklistService.GetBlacklistedDirectories(profileId);

            // Extract path from each Blacklist object into a List<string>
            var blacklistedPathsList = blacklistedPaths.Select(b => b.Path).ToList();

            // Remove directory slashes to match returned value of Path.GetDirectoryName() method
            var normalizedBlacklist = blacklistedPathsList
                .Select(p => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToHashSet();

            // Return tracks that are not in blacklisted folders
            return allTracksToValidate.Where(t => !normalizedBlacklist.Contains(Path.GetDirectoryName(t.FilePath))).ToList();

        }

        public async Task<List<Albums>> GetAlbumsForProfile()
        {
            var tempAllAlbums = await _albumRepository.GetAllAlbums();

            var trackAlbumIds = AllTracks.Select(t => t.AlbumID).ToHashSet(); // Get unique AlbumIds from tracks

            return tempAllAlbums.Where(album => trackAlbumIds.Contains(album.AlbumID)).ToList(); // Filter only albums present in tracks
        }

        public async Task<List<Artists>> GetArtistsForProfile()
        {
            var tempAllArtists = await _artistsRepository.GetAllArtists();

            var trackArtistIds = AllTracks.SelectMany(t => t.Artists.Select(a => a.ArtistID)).ToHashSet(); //  Extract all artist IDs from tracks

            return tempAllArtists.Where(artist => trackArtistIds.Contains(artist.ArtistID)).ToList(); // Filter only artists present in tracks
        }

        public async Task<List<Genres>> GetGenresForProfile()
        {
            var tempAllGenres = await _genresRepository.GetAllGenres();

            var trackGenres = AllTracks.Select(t => t.Genre).ToHashSet(); // Get unique Genres from tracks

            return tempAllGenres.Where(genres => trackGenres.Contains(genres.GenreName)).ToList(); // Filter only genres present in tracks
        }

    }
}
