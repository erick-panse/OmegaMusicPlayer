using Npgsql;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Profile
{
    public class ProfileRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ConcurrentDictionary<int, Profiles> _profileCache = new ConcurrentDictionary<int, Profiles>();
        private readonly List<Profiles> _allProfilesCache = new List<Profiles>();
        private DateTime _allProfilesCacheTime = DateTime.MinValue;
        private const int CACHE_EXPIRY_MINUTES = 5;

        public ProfileRepository(
            IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Retrieves a profile by ID with fallback to cached version if available.
        /// </summary>
        public async Task<Profiles> GetProfileById(int profileID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Profile WHERE ProfileID = @profileID";
                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("profileID", profileID);

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            var profile = new Profiles
                            {
                                ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                                ProfileName = reader.GetString(reader.GetOrdinal("ProfileName")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                                PhotoID = reader.IsDBNull(reader.GetOrdinal("PhotoID")) ? 0 : reader.GetInt32(reader.GetOrdinal("PhotoID"))
                            };

                            // Update cache
                            _profileCache[profileID] = profile;
                            return profile;
                        }
                        return null;
                    }
                },
                $"Getting profile with ID {profileID}",
                _profileCache.TryGetValue(profileID, out var cachedProfile) ? cachedProfile : null,
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Retrieves all profiles with fallback to cached list.
        /// </summary>
        public async Task<List<Profiles>> GetAllProfiles()
        {
            // Return cache if it's still fresh
            if (_allProfilesCache.Count > 0 &&
                DateTime.Now.Subtract(_allProfilesCacheTime).TotalMinutes < CACHE_EXPIRY_MINUTES)
            {
                return _allProfilesCache;
            }

            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profiles = new List<Profiles>();
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Profile ORDER BY CreatedAt DESC";
                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var profile = new Profiles
                            {
                                ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                                ProfileName = reader.GetString(reader.GetOrdinal("ProfileName")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                                PhotoID = reader.IsDBNull(reader.GetOrdinal("PhotoID")) ? 0 : reader.GetInt32(reader.GetOrdinal("PhotoID"))
                            };

                            profiles.Add(profile);
                            _profileCache[profile.ProfileID] = profile;
                        }
                    }

                    // Update the "all profiles" cache
                    _allProfilesCache.Clear();
                    _allProfilesCache.AddRange(profiles);
                    _allProfilesCacheTime = DateTime.Now;

                    return profiles;
                },
                "Getting all profiles",
                _allProfilesCache.Count > 0 ? _allProfilesCache : null,
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Creates a new profile with proper error handling and transaction management.
        /// Also creates the associated ProfileConfig.
        /// </summary>
        public async Task<int> AddProfile(Profiles profile)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // Create profile
                            string profileQuery = @"
                                INSERT INTO Profile (ProfileName, CreatedAt, UpdatedAt, PhotoID)
                                VALUES (@profileName, @createdAt, @updatedAt, @photoID)
                                RETURNING ProfileID";

                            using var cmd = new NpgsqlCommand(profileQuery, db.dbConn, transaction);
                            cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                            cmd.Parameters.AddWithValue("createdAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("photoID", profile.PhotoID > 0 ? profile.PhotoID : DBNull.Value);

                            var profileId = (int)await cmd.ExecuteScalarAsync();

                            transaction.Commit();

                            // Update the profile object and cache it
                            profile.ProfileID = profileId;
                            _profileCache[profileId] = profile;

                            // Invalidate the all profiles cache to force refresh
                            _allProfilesCacheTime = DateTime.MinValue;

                            return profileId;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to create new profile",
                                ex.Message,
                                ex);
                            throw;
                        }
                    }
                },
                $"Creating new profile '{profile.ProfileName}'",
                -1, // Return -1 to indicate error
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates an existing profile with error handling.
        /// </summary>
        public async Task UpdateProfile(Profiles profile)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE Profile 
                            SET ProfileName = @profileName,
                                UpdatedAt = @updatedAt,
                                PhotoID = @photoID
                            WHERE ProfileID = @profileID";

                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("profileID", profile.ProfileID);
                        cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                        cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("photoID", profile.PhotoID > 0 ? profile.PhotoID : DBNull.Value);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            // Update the cached profile
                            _profileCache[profile.ProfileID] = profile;

                            // Invalidate the all profiles cache to force refresh
                            _allProfilesCacheTime = DateTime.MinValue;
                        }
                    }
                },
                $"Updating profile {profile.ProfileID}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Deletes a profile with error handling.
        /// </summary>
        public async Task DeleteProfile(int profileID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // We're using ON DELETE CASCADE for ProfileConfig, so we only need to delete the profile
                        string profileQuery = "DELETE FROM Profile WHERE ProfileID = @profileID";
                        using var profileCmd = new NpgsqlCommand(profileQuery, db.dbConn);
                        profileCmd.Parameters.AddWithValue("profileID", profileID);
                        await profileCmd.ExecuteNonQueryAsync();

                        // Remove from cache
                        _profileCache.TryRemove(profileID, out _);

                        // Invalidate the all profiles cache to force refresh
                        _allProfilesCacheTime = DateTime.MinValue;
                    }
                },
                $"Deleting profile {profileID}",
                ErrorSeverity.NonCritical);
        }
    }
}