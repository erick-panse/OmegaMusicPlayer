using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class BlacklistedDirectoryRepository
    {
        public async Task<List<BlacklistedDirectory>> GetByProfile(int profileId)
        {
            var directories = new List<BlacklistedDirectory>();

            using (var db = new DbConnection())
            {
                string query = "SELECT * FROM BlacklistedDirectories WHERE ProfileID = @ProfileID";
                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("ProfileID", profileId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    directories.Add(new BlacklistedDirectory
                    {
                        BlacklistID = reader.GetInt32(reader.GetOrdinal("BlacklistID")),
                        ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                        Path = reader.GetString(reader.GetOrdinal("Path"))
                    });
                }
            }
            return directories;
        }

        public async Task<int> Add(BlacklistedDirectory directory)
        {
            using (var db = new DbConnection())
            {
                string query = @"
                    INSERT INTO BlacklistedDirectories (ProfileID, Path)
                    VALUES (@ProfileID, @Path)
                    RETURNING BlacklistID";

                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("ProfileID", directory.ProfileID);
                cmd.Parameters.AddWithValue("Path", directory.Path);

                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        public async Task Remove(int blacklistId)
        {
            using (var db = new DbConnection())
            {
                string query = "DELETE FROM BlacklistedDirectories WHERE BlacklistID = @BlacklistID";
                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("BlacklistID", blacklistId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}