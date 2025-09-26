using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Infrastructure.Data.Repositories.Library
{
    public class DirectoriesRepository
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public DirectoriesRepository(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService = null)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Gets a directory by ID
        /// </summary>
        public async Task<Directories> GetDirectoryById(int dirID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var directory = await context.Directories
                        .AsNoTracking()
                        .Where(d => d.DirId == dirID)
                        .Select(d => new Directories
                        {
                            DirID = d.DirId,
                            DirPath = d.DirPath
                        })
                        .FirstOrDefaultAsync();

                    return directory;
                },
                $"Getting directory with ID {dirID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Gets all directories
        /// </summary>
        public async Task<List<Directories>> GetAllDirectories()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var directories = await context.Directories
                        .AsNoTracking()
                        .OrderBy(d => d.DirPath)
                        .Select(d => new Directories
                        {
                            DirID = d.DirId,
                            DirPath = d.DirPath
                        })
                        .ToListAsync();

                    return directories;
                },
                "Getting all directories",
                new List<Directories>(),
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Adds a directory
        /// </summary>
        public async Task<int> AddDirectory(Directories directory)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (directory == null || string.IsNullOrWhiteSpace(directory.DirPath))
                    {
                        throw new ArgumentException("Directory path cannot be null or empty");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    // Check for duplicate first to avoid unique constraint violations
                    var exists = await context.Directories
                        .AnyAsync(d => EF.Functions.ILike(d.DirPath, directory.DirPath));

                    if (exists)
                    {
                        throw new InvalidOperationException($"Directory path already exists: {directory.DirPath}");
                    }

                    var newDirectory = new Infrastructure.Data.Entities.Directory
                    {
                        DirPath = directory.DirPath
                    };

                    context.Directories.Add(newDirectory);
                    await context.SaveChangesAsync();

                    return newDirectory.DirId;
                },
                $"Adding directory: {directory?.DirPath ?? "null"}",
                -1, // Return -1 on error
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Deletes a directory
        /// </summary>
        public async Task DeleteDirectory(int dirID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var directory = await context.Directories
                        .Where(d => d.DirId == dirID)
                        .FirstOrDefaultAsync();

                    if (directory == null)
                    {
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Directory not found",
                            $"Directory with ID {dirID} was not found in the database.",
                            null,
                            false);
                        return;
                    }

                    context.Directories.Remove(directory);
                    await context.SaveChangesAsync();
                },
                $"Deleting directory with ID {dirID}",
                ErrorSeverity.NonCritical,
                false);
        }
    }
}