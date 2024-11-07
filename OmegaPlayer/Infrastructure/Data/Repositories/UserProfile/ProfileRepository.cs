using Npgsql;
using OmegaPlayer.Features.UserProfile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.UserProfile
{
    public class ProfileRepository
    {
        public async Task<Profile> GetProfileById(int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Profile WHERE profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", profileID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Profile
                                {
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    ProfileName = reader.GetString(reader.GetOrdinal("profileName")),
                                    ConfigID = reader.GetInt32(reader.GetOrdinal("configID")),
                                    PhotoID = reader.GetInt32(reader.GetOrdinal("photoID")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the Profile by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Profile>> GetAllProfiles()
        {
            var profiles = new List<Profile>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Profile";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var profile = new Profile
                                {
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    ProfileName = reader.GetString(reader.GetOrdinal("profileName")),
                                    ConfigID = reader.GetInt32(reader.GetOrdinal("configID")),
                                    PhotoID = reader.GetInt32(reader.GetOrdinal("photoID")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };

                                profiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all Profiles: {ex.Message}");
                throw;
            }

            return profiles;
        }

        public async Task<int> AddProfile(Profile profile)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Profile (profileName, configID, photoID, createdAt, updatedAt)
                        VALUES (@profileName, @configID, @photoID, @createdAt, @updatedAt) RETURNING profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                        cmd.Parameters.AddWithValue("configID", profile.ConfigID);
                        cmd.Parameters.AddWithValue("photoID", profile.PhotoID);
                        cmd.Parameters.AddWithValue("createdAt", profile.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", profile.UpdatedAt);

                        var profileID = (int)cmd.ExecuteScalar();
                        return profileID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the Profile: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateProfile(Profile profile)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Profile SET 
                            profileName = @profileName,
                            configID = @configID,
                            photoID = @photoID,
                            updatedAt = @updatedAt
                        WHERE profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", profile.ProfileID);
                        cmd.Parameters.AddWithValue("profileName", profile.ProfileName);
                        cmd.Parameters.AddWithValue("configID", profile.ConfigID);
                        cmd.Parameters.AddWithValue("photoID", profile.PhotoID);
                        cmd.Parameters.AddWithValue("updatedAt", profile.UpdatedAt);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the Profile: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteProfile(int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Profile WHERE profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", profileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the Profile: {ex.Message}");
                throw;
            }
        }
    }
}
