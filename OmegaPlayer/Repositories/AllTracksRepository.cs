using OmegaPlayer.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class AllTracksRepository
    {
        public List<TrackDisplayModel> AllTracks { get; private set; } = new();

        private readonly TrackDisplayService _trackDisplayService;

        public AllTracksRepository(TrackDisplayService trackDisplayService)
        {
            _trackDisplayService = trackDisplayService;
            LoadTracks();
        }

        public async Task LoadTracks()
        {
            AllTracks = await _trackDisplayService.GetAllTracksWithMetadata(2); // Fetch once
        }
    }
}
