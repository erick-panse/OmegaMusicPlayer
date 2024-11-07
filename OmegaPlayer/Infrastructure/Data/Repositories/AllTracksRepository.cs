using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class AllTracksRepository
    {
        public List<TrackDisplayModel> AllTracks { get; private set; } = new();

        private readonly TrackDisplayRepository _trackDisplayRepository;

        public AllTracksRepository(TrackDisplayRepository trackDisplayRepository)
        {
            _trackDisplayRepository = trackDisplayRepository;
            LoadTracks();
        }

        public async Task LoadTracks()
        {
            AllTracks = await _trackDisplayRepository.GetAllTracksWithMetadata(2); // Fetch once
        }
    }
}
