using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Profile.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT profileid, profilename, createdat, updatedat, photoid FROM profile WHERE profileid = @profileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileID"] = profileID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var profile = new Profiles
                            {
                                ProfileID = reader.GetInt32("profileid"),
                                ProfileName = reader.GetString("profilename"),
                                CreatedAt = reader.GetDateTime("createdat"),
                                UpdatedAt = reader.GetDateTime("updatedat"),
                                PhotoID = reader.IsDBNull("photoid") ? 0 : reader.GetInt32("photoid")
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
                        string query = "SELECT profileid, profilename, createdat, updatedat, photoid FROM profile ORDER BY createdat DESC";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var profile = new Profiles
                            {
                                ProfileID = reader.GetInt32("profileid"),
                                ProfileName = reader.GetString("profilename"),
                                CreatedAt = reader.GetDateTime("createdat"),
                                UpdatedAt = reader.GetDateTime("updatedat"),
                                PhotoID = reader.IsDBNull("photoid") ? 0 : reader.GetInt32("photoid")
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
                            // Create profile - SQLite transactions work with the existing connection
                            string profileQuery = @"
                                INSERT INTO profile (profilename, createdat, updatedat, photoid)
                                VALUES (@profileName, @createdAt, @updatedAt, @photoID)";

                            var parameters = new Dictionary<string, object>
                            {
                                ["@profileName"] = profile.ProfileName,
                                ["@createdAt"] = DateTime.Now,
                                ["@updatedAt"] = DateTime.Now,
                                ["@photoID"] = profile.PhotoID > 0 ? profile.PhotoID : null
                            };

                            using var cmd = db.CreateCommand(profileQuery, parameters);
                            cmd.Transaction = transaction;
                            await cmd.ExecuteNonQueryAsync();

                            // Get the inserted profile ID
                            using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                            idCmd.Transaction = transaction;
                            var result = await idCmd.ExecuteScalarAsync();
                            var profileId = Convert.ToInt32(result);

                            // Create default profile configuration
                            string configQuery = @"
                                INSERT INTO profileconfig (profileid, equalizerpresets, lastvolume, theme, dynamicpause, 
                                                          blacklistdirectory, viewstate, sortingstate)
                                VALUES (@ProfileID, @EqualizerPresets, @LastVolume, @Theme, @DynamicPause, 
                                       @BlacklistDirectory, @ViewState, @SortingState)";

                            var configParameters = new Dictionary<string, object>
                            {
                                ["@ProfileID"] = profileId,
                                ["@EqualizerPresets"] = "{}",
                                ["@LastVolume"] = 50,
                                ["@Theme"] = "dark",
                                ["@DynamicPause"] = true,
                                ["@BlacklistDirectory"] = "[]",
                                ["@ViewState"] = "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                                ["@SortingState"] = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
                            };

                            using var configCmd = db.CreateCommand(configQuery, configParameters);
                            configCmd.Transaction = transaction;
                            await configCmd.ExecuteNonQueryAsync();

                            transaction.Commit();

                            // Update the profile object and cache it
                            profile.ProfileID = profileId;
                            profile.CreatedAt = DateTime.Now;
                            profile.UpdatedAt = DateTime.Now;
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
                            UPDATE profile 
                            SET profilename = @profileName,
                                updatedat = @updatedAt,
                                photoid = @photoID
                            WHERE profileid = @profileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileID"] = profile.ProfileID,
                            ["@profileName"] = profile.ProfileName,
                            ["@updatedAt"] = DateTime.Now,
                            ["@photoID"] = profile.PhotoID > 0 ? profile.PhotoID : null
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            // Update the cached profile
                            profile.UpdatedAt = DateTime.Now;
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
        /// Note: ProfileConfig and other related data will be deleted automatically due to CASCADE constraints.
        /// </summary>
        public async Task DeleteProfile(int profileID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // SQLite with foreign key constraints enabled will handle CASCADE deletes
                        string profileQuery = "DELETE FROM profile WHERE profileid = @profileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileID"] = profileID
                        };

                        using var profileCmd = db.CreateCommand(profileQuery, parameters);
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