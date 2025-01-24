using Npgsql;
using OmegaPlayer.Features.Profile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Profile
{
    public class UserActivityRepository
    {
        public async Task<UserActivity> GetUserActivityById(int userActivityID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM UserActivity WHERE userActivityID = @userActivityID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("userActivityID", userActivityID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserActivity
                                {
                                    UserActivityID = reader.GetInt32(reader.GetOrdinal("userActivityID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    ActivityTime = reader.GetDateTime(reader.GetOrdinal("activityTime")),
                                    ActivityType = reader.GetString(reader.GetOrdinal("activityType"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the UserActivity by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<UserActivity>> GetAllUserActivities()
        {
            var userActivities = new List<UserActivity>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM UserActivity";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var userActivity = new UserActivity
                                {
                                    UserActivityID = reader.GetInt32(reader.GetOrdinal("userActivityID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    ActivityTime = reader.GetDateTime(reader.GetOrdinal("activityTime")),
                                    ActivityType = reader.GetString(reader.GetOrdinal("activityType"))
                                };

                                userActivities.Add(userActivity);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all UserActivities: {ex.Message}");
                throw;
            }

            return userActivities;
        }

        public async Task AddUserActivity(UserActivity userActivity)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO UserActivity (trackID, profileID, activityTime, activityType)
                        VALUES (@trackID, @profileID, @activityTime, @activityType)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", userActivity.TrackID);
                        cmd.Parameters.AddWithValue("profileID", userActivity.ProfileID);
                        cmd.Parameters.AddWithValue("activityTime", userActivity.ActivityTime);
                        cmd.Parameters.AddWithValue("activityType", userActivity.ActivityType);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the UserActivity: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteUserActivity(int userActivityID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM UserActivity WHERE userActivityID = @userActivityID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("userActivityID", userActivityID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the UserActivity: {ex.Message}");
                throw;
            }
        }
    }
}
