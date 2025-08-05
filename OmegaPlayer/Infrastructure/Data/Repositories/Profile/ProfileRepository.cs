using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Profile.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Profile
{
    public class ProfileRepository
    {
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ConcurrentDictionary<int, Profiles> _profileCache = new ConcurrentDictionary<int, Profiles>();
        private readonly List<Profiles> _allProfilesCache = new List<Profiles>();
        private DateTime _allProfilesCacheTime = DateTime.MinValue;
        private const int CACHE_EXPIRY_MINUTES = 5;

        public ProfileRepository(
            ProfileConfigRepository profileConfigRepository,
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _profileConfigRepository = profileConfigRepository;
            _contextFactory = contextFactory;
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
                    using var context = _contextFactory.CreateDbContext();

                    var profile = await context.Profiles
                        .AsNoTracking()
                        .Where(p => p.ProfileId == profileID)
                        .Select(p => new Profiles
                        {
                            ProfileID = p.ProfileId,
                            ProfileName = p.ProfileName,
                            CreatedAt = p.CreatedAt,
                            UpdatedAt = p.UpdatedAt,
                            PhotoID = p.PhotoId ?? 0
                        })
                        .FirstOrDefaultAsync();

                    if (profile != null)
                    {
                        // Update cache
                        _profileCache[profileID] = profile;
                    }

                    return profile;
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
                    using var context = _contextFactory.CreateDbContext();

                    var profiles = await context.Profiles
                        .AsNoTracking()
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => new Profiles
                        {
                            ProfileID = p.ProfileId,
                            ProfileName = p.ProfileName,
                            CreatedAt = p.CreatedAt,
                            UpdatedAt = p.UpdatedAt,
                            PhotoID = p.PhotoId ?? 0
                        })
                        .ToListAsync();

                    // Update individual profile cache
                    foreach (var profile in profiles)
                    {
                        _profileCache[profile.ProfileID] = profile;
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
                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // Create profile
                        var newProfile = new Entities.Profile
                        {
                            ProfileName = profile.ProfileName,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            PhotoId = profile.PhotoID > 0 ? profile.PhotoID : null
                        };

                        context.Profiles.Add(newProfile);
                        await context.SaveChangesAsync();

                        var profileId = newProfile.ProfileId;

                        // Create default profile configuration
                        var newProfileConfig = new Entities.ProfileConfig
                        {
                            ProfileId = profileId,
                            EqualizerPresets = "{}",
                            LastVolume = 50,
                            Theme = _profileConfigRepository.DefaultTheme, // get default value from repository
                            DynamicPause = false,
                            BlacklistDirectory = Array.Empty<string>(),
                            ViewState = _profileConfigRepository.DefaultViewState, // get default value from repository
                            SortingState = _profileConfigRepository.DefaultSortingState // get default value from repository
                        };

                        context.ProfileConfigs.Add(newProfileConfig);
                        await context.SaveChangesAsync();

                        await transaction.CommitAsync();

                        // Update the profile object and cache it
                        profile.ProfileID = profileId;
                        profile.CreatedAt = DateTime.UtcNow;
                        profile.UpdatedAt = DateTime.UtcNow;
                        _profileCache[profileId] = profile;

                        // Invalidate the all profiles cache to force refresh
                        _allProfilesCacheTime = DateTime.MinValue;

                        return profileId;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to create new profile",
                            ex.Message,
                            ex);
                        throw;
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
                    using var context = _contextFactory.CreateDbContext();

                    var existingProfile = await context.Profiles
                        .Where(p => p.ProfileId == profile.ProfileID)
                        .FirstOrDefaultAsync();

                    if (existingProfile != null)
                    {
                        existingProfile.ProfileName = profile.ProfileName;
                        existingProfile.UpdatedAt = DateTime.UtcNow;
                        existingProfile.PhotoId = profile.PhotoID > 0 ? profile.PhotoID : null;

                        await context.SaveChangesAsync();

                        // Update the cached profile
                        profile.UpdatedAt = DateTime.UtcNow;
                        _profileCache[profile.ProfileID] = profile;

                        // Invalidate the all profiles cache to force refresh
                        _allProfilesCacheTime = DateTime.MinValue;
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
                    using var context = _contextFactory.CreateDbContext();

                    // EF Core with foreign key constraints enabled will handle CASCADE deletes
                    await context.Profiles
                        .Where(p => p.ProfileId == profileID)
                        .ExecuteDeleteAsync();

                    // Remove from cache
                    _profileCache.TryRemove(profileID, out _);

                    // Invalidate the all profiles cache to force refresh
                    _allProfilesCacheTime = DateTime.MinValue;
                },
                $"Deleting profile {profileID}",
                ErrorSeverity.NonCritical);
        }
    }
}