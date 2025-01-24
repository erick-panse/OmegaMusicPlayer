using Npgsql;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Profile.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Profile
{
    public class BlackListProfileRepository
    {
        public async Task<BlackListProfile> GetBlackListProfile(int blackListID, int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM BlackListProfile WHERE blackListID = @blackListID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("blackListID", blackListID);
                        cmd.Parameters.AddWithValue("profileID", profileID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new BlackListProfile
                                {
                                    BlackListID = reader.GetInt32(reader.GetOrdinal("blackListID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the BlackListProfile: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<BlackListProfile>> GetAllBlackListProfiles()
        {
            var blackListProfiles = new List<BlackListProfile>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM BlackListProfile";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var blackListProfile = new BlackListProfile
                                {
                                    BlackListID = reader.GetInt32(reader.GetOrdinal("blackListID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID"))
                                };

                                blackListProfiles.Add(blackListProfile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all BlackListProfiles: {ex.Message}");
                throw;
            }

            return blackListProfiles;
        }

        public async Task AddBlackListProfile(BlackListProfile blackListProfile)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "INSERT INTO BlackListProfile (blackListID, profileID) VALUES (@blackListID, @profileID)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("blackListID", blackListProfile.BlackListID);
                        cmd.Parameters.AddWithValue("profileID", blackListProfile.ProfileID);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the BlackListProfile: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteBlackListProfile(int blackListID, int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM BlackListProfile WHERE blackListID = @blackListID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("blackListID", blackListID);
                        cmd.Parameters.AddWithValue("profileID", profileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the BlackListProfile: {ex.Message}");
                throw;
            }
        }
    }
}
