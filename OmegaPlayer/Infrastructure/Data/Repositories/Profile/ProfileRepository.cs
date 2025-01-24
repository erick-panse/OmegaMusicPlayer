using Npgsql;
using OmegaPlayer.Features.Profile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Profile
{
    public class ProfileRepository
    {
        public async Task<Profiles> GetProfileById(int profileID)
        {
            using (var db = new DbConnection())
            {
                string query = "SELECT * FROM Profile WHERE ProfileID = @profileID";
                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("profileID", profileID);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Profiles
                    {
                        ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                        ProfileName = reader.GetString(reader.GetOrdinal("ProfileName")),
                        ConfigID = reader.GetInt32(reader.GetOrdinal("ConfigID")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        PhotoID = reader.IsDBNull(reader.GetOrdinal("PhotoID")) ? 0 : reader.GetInt32(reader.GetOrdinal("PhotoID"))
                    };
                }
            }
            return null;
        }

        public async Task<List<Profiles>> GetAllProfiles()
        {
            var profiles = new List<Profiles>();
            using (var db = new DbConnection())
            {
                string query = "SELECT * FROM Profile ORDER BY CreatedAt DESC";
                using var cmd = new NpgsqlCommand(query, db.dbConn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    profiles.Add(new Profiles
                    {
                        ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                        ProfileName = reader.GetString(reader.GetOrdinal("ProfileName")),
                        ConfigID = reader.GetInt32(reader.GetOrdinal("ConfigID")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                        PhotoID = reader.IsDBNull(reader.GetOrdinal("PhotoID")) ? 0 : reader.GetInt32(reader.GetOrdinal("PhotoID"))
                    });
                }
            }
            return profiles;
        }

        //public async Task<int> AddProfile(Profiles profile) // correct version
        //{
        //    using (var db = new DbConnection())
        //    {
        //        string query = @"
        //            INSERT INTO Profile (ProfileName, ConfigID, CreatedAt, UpdatedAt, PhotoID)
        //            VALUES (@profileName, @configID, @createdAt, @updatedAt, @photoID)
        //            RETURNING ProfileID";

        //        using var cmd = new NpgsqlCommand(query, db.dbConn);
        //        cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
        //        cmd.Parameters.AddWithValue("configID", profile.ConfigID);
        //        cmd.Parameters.AddWithValue("createdAt", DateTime.Now);
        //        cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);
        //        cmd.Parameters.AddWithValue("photoID", profile.PhotoID);

        //        return (int)await cmd.ExecuteScalarAsync();
        //    }
        //}

        // Test version while there is no config in the app
        public async Task<int> AddProfile(Profiles profile)
        {
            using (var db = new DbConnection())
            {
                using var transaction = db.dbConn.BeginTransaction();
                try
                {
                    // First create default config
                    string configQuery = "INSERT INTO Config DEFAULT VALUES RETURNING ConfigID";
                    using var configCmd = new NpgsqlCommand(configQuery, db.dbConn, transaction);
                    var configId = (int)await configCmd.ExecuteScalarAsync();

                    // Then create profile with the new config
                    string profileQuery = @"
                INSERT INTO Profile (ProfileName, ConfigID, CreatedAt, UpdatedAt, PhotoID)
                VALUES (@profileName, @configID, @createdAt, @updatedAt, @photoID)
                RETURNING ProfileID";
                    using var cmd = new NpgsqlCommand(profileQuery, db.dbConn, transaction);
                    cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                    cmd.Parameters.AddWithValue("configID", configId);
                    cmd.Parameters.AddWithValue("createdAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("photoID", profile.PhotoID > 0 ? profile.PhotoID : DBNull.Value);

                    var profileId = (int)await cmd.ExecuteScalarAsync();
                    transaction.Commit();
                    return profileId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public async Task UpdateProfile(Profiles profile)
        {
            using (var db = new DbConnection())
            {
                string query = @"
                    UPDATE Profile 
                    SET ProfileName = @profileName,
                        ConfigID = @configID,
                        UpdatedAt = @updatedAt,
                        PhotoID = @photoID
                    WHERE ProfileID = @profileID";

                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("profileID", profile.ProfileID);
                cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                cmd.Parameters.AddWithValue("configID", profile.ConfigID);
                cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);
                cmd.Parameters.AddWithValue("photoID", profile.PhotoID > 0 ? profile.PhotoID : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteProfile(int profileID)
        {
            using (var db = new DbConnection())
            {
                string query = "DELETE FROM Profile WHERE ProfileID = @profileID";
                using var cmd = new NpgsqlCommand(query, db.dbConn);
                cmd.Parameters.AddWithValue("profileID", profileID);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}