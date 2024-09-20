using OmegaPlayer.Models;
using Genres = OmegaPlayer.Models.Genres;
using File = System.IO.File;
using System.Linq;
using System;
using TagLib;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.ComponentModel;

namespace OmegaPlayer.Services
{
    public class TrackMetadataService
    {
        private readonly TracksService _trackService;
        private readonly ArtistsService _artistService;
        private readonly AlbumService _albumService;
        private readonly GenresService _genreService;
        private readonly MediaService _mediaService;
        private readonly TrackArtistService _trackArtistService;
        private readonly TrackGenreService _trackGenreService;

        public TrackMetadataService(
            TracksService trackService,
            ArtistsService artistService,
            AlbumService albumService,
            GenresService genreService,
            MediaService mediaService,
            TrackArtistService trackArtistService,
            TrackGenreService trackGenreService)
        {
            _trackService = trackService;
            _artistService = artistService;
            _albumService = albumService;
            _genreService = genreService;
            _mediaService = mediaService;
            _trackArtistService = trackArtistService;
            _trackGenreService = trackGenreService;
        }

        public async Task PopulateTrackMetadata(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("The specified track file does not exist.");
                }

                var file = TagLib.File.Create(filePath);

                //Check if track already exists and creates Track obj to populate later on - Do not change unless needed
                Tracks track = await _trackService.GetTrackByPath(filePath) ?? new Tracks();

                // Handle Artist
                var filesArtistName = file.Tag.Performers.First();
                List<Artists> artists = await _artistService.GetArtistByName(filesArtistName) ?? new List<Artists>();// get artist with this name, even if there is more than one artist with the same name
                var artist = new Artists(); // where selected artist will be inserted

                if (artists.Count == 1)
                {
                    // If only one artist is found, use it
                    artist = artists.First();
                }
                else if (artists.Count > 1)
                {
                    // Handle multiple artists with the same name (currently impossible, implement adjustments later when using API)
                    // For simplicity, let's select the first one or prompt user to select
                    // replace this with a logic to choose the correct artist

                    // Ideas: Using the first one or prompting the user (not implemented)
                    artist = artists.First(); 
                }

                if (filesArtistName.Length > 0 && artists.Count == 0)
                {
                    artist.ArtistName = filesArtistName;
                    artist.CreatedAt = DateTime.Now;
                    artist.UpdatedAt = DateTime.Now;

                    artist.ArtistID = await _artistService.AddArtist(artist); //Insert the data into the DB and generates an ID
                }



                // Handle Media to Get CoverID Then Album
                var albumTag = file.Tag.Album;
                Albums album = await _albumService.GetAlbumByTitle(albumTag, artist.ArtistID) ?? new Albums();

                // Handle Media (Album cover, etc.)
                if (track.CoverID == 0)
                {
                    var media = await SaveMedia(file, filePath, "track_cover");
                    track.CoverID = media.MediaID;
                    album.CoverID = media.MediaID; // check if album cover already exists
                }

                // Handle Album
                if (!string.IsNullOrEmpty(albumTag) && album.AlbumID == 0)
                {
                    // Improve using MusicBrainz API later
                    album.Title = file.Tag.Album;
                    //album.ReleaseDate = file.Tag.Year > 0 ? new DateTime((int)file.Tag.Year, 1, 1) : (DateTime?)null;
                    album.CreatedAt = DateTime.Now;
                    album.UpdatedAt = DateTime.Now;
                    album.ArtistID = artist != null ? artist.ArtistID : 0; //Insert artistId and if it does not exists insert 0 (unknown artist)
                    album.AlbumID = await _albumService.AddAlbum(album); //Insert the data into the DB and generates an ID

                }
                track.AlbumID = album != null ? album.AlbumID : 0; // adds AlbumID to the track

                // Handle Genre
                var genreName = file.Tag.Genres.First();
                Genres genre = await _genreService.GetGenreByName(genreName) ?? new Genres();

                if (file.Tag.Genres.Length > 0 && genre.GenreID == 0)
                {
                    genre.GenreName = genreName;

                    genre.GenreID = await _genreService.AddGenre(genre);
                }
                track.GenreID = genre != null ? genre.GenreID : 0;


                // Implement API to get the artist photo

                //if (artist.PhotoID == 0)
                //{
                //    var media = SaveMedia(file, filePath, "artist_photo");
                //    artist.PhotoID = media.MediaID;
                //}
                if (album.CoverID == 0)
                {
                    var media = await SaveMedia(file, filePath, "album_cover");
                    album.CoverID = media.MediaID;
                }

                // Handle Track after creating / finding the albumID
                if (track.TrackID == 0)
                {
                    track.FilePath = filePath;
                    track.Title = string.IsNullOrEmpty(file.Tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : file.Tag.Title;
                    track.Duration = file.Properties.Duration;
                    track.BitRate = file.Properties.AudioBitrate;
                    track.FileSize = (int)new FileInfo(filePath).Length;
                    track.FileType = Path.GetExtension(filePath)?.TrimStart('.');
                    track.CreatedAt = DateTime.Now;
                    track.UpdatedAt = DateTime.Now;
                    track.PlayCount = 0;// Initialize play count to 0

                    track.TrackID = await _trackService.AddTrack(track); //Insert the data into the DB and generates an ID
                }

                // Associate Track with Artist and Genre
                var trackID = track.TrackID;
                var artistID = artist.ArtistID;
                var genreID = genre.GenreID;

                var trackArtist = await _trackArtistService.GetTrackArtist(trackID, artistID) ?? new TrackArtist();
                if (artist.ArtistID != 0 && trackArtist.ArtistID == 0 && trackArtist.TrackID == 0)
                {
                    trackArtist.TrackID = track.TrackID;
                    trackArtist.ArtistID = artist.ArtistID;

                    await _trackArtistService.AddTrackArtist(trackArtist);
                }

                var trackGenre = await _trackGenreService.GetTrackGenre(trackID, genreID) ?? new TrackGenre();
                if (genre.GenreID != 0 && trackGenre.GenreID == 0 && trackGenre.TrackID == 0)
                {
                    trackGenre.TrackID = track.TrackID;
                    trackGenre.GenreID = genre.GenreID;

                    await _trackGenreService.AddTrackGenre(trackGenre);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while populating track metadata: {ex.Message}");
                throw;
            }
        }

        private async Task<string> SaveImage(Stream imageStream, string mediaType, int mediaID)
        {
            var projectBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Define the base directory for all media files
            var baseDirectory = Path.Combine(projectBaseDirectory, "media", mediaType);

            // Create subdirectories based on mediaType and mediaID
            var subDirectory = Path.Combine(baseDirectory, mediaID.ToString("D7")); // Ensures 001, 002, etc. subfolder for each track for more performance

            // Ensure the directory exists, or create it
            if (!Directory.Exists(subDirectory))
            {
                Directory.CreateDirectory(subDirectory);
            }

            // Set the file name depending on the mediaType
            string fileName;
            switch (mediaType)
            {
                case "track_cover":
                    fileName = $"track_{mediaID.ToString("D7")}_cover.jpg";
                    break;
                case "album_cover":
                    fileName = $"album_{mediaID.ToString("D7")}_cover.jpg";
                    break;
                case "artist_photo":
                    fileName = $"artist_{mediaID.ToString("D7")}_photo.jpg";
                    break;
                default:
                    throw new ArgumentException("Invalid media type");
            }

            // Combine the directory and file name
            var imagePath = Path.Combine(subDirectory, fileName);

            // Save the image to the specified path
            using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write))
            {
                imageStream.CopyTo(fileStream);
            }

            return imagePath;
        }

        public async Task<Media> SaveMedia(TagLib.File file, string filePath, string mediaType)
        {
            Media media = null;
            if (file.Tag.Pictures.Length > 0)
            {
                var picture = file.Tag.Pictures.First();

                // Step 1: Insert media without the file path and retrieve the MediaID
                media = new Media
                {
                    CoverPath = null, // Will be set later after saving the image
                    MediaType = mediaType
                };

                // Insert into the database and get MediaID
                int mediaId = await _mediaService.AddMedia(media);
                media.MediaID = mediaId;

                // Step 2: Save the image with MediaID and update the file path
                using (var ms = new MemoryStream(picture.Data.Data))
                {
                    var imageFilePath = await SaveImage(ms, mediaType, mediaId);
                    media.CoverPath = imageFilePath;
                }

                // Step 3: Update the Media record with the correct file path
                await _mediaService.UpdateMediaFilePath(mediaId, media.CoverPath);

            }
            return media;
        }
        public async Task UpdateTrackMetada(string filePath)
        {
            //TODO
        }
    }
}
