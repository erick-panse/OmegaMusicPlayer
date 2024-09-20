using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class GenresService
    {
        private readonly GenresRepository _genresRepository;

        public GenresService()
        {
            _genresRepository = new GenresRepository();
        }

        public async Task<Genres> GetGenreByName(string genreName)
        {
            try
            {
                return await _genresRepository.GetGenreByName(genreName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching genre by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Genres>> GetAllGenres()
        {
            try
            {
                return await _genresRepository.GetAllGenres();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all genres: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddGenre(Genres genre)
        {
            try
            {
                return await _genresRepository.AddGenre(genre);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding genre: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateGenre(Genres genre)
        {
            try
            {
                await _genresRepository.UpdateGenre(genre);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating genre: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteGenre(int genreID)
        {
            try
            {
                await _genresRepository.DeleteGenre(genreID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting genre: {ex.Message}");
                throw;
            }
        }
    }
}
