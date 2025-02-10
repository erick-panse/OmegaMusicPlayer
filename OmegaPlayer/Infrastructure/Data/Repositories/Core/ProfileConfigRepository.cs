using Npgsql;
using OmegaPlayer.Core.Models;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{

    public class ProfileConfigRepository
    {
        public async Task<ProfileConfig> GetProfileConfig(int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM ProfileConfig WHERE ProfileID = @ProfileID";
                    using var cmd = new NpgsqlCommand(query, db.dbConn);
                    cmd.Parameters.AddWithValue("ProfileID", profileId);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        return new ProfileConfig
                        {
                            ID = reader.GetInt32(reader.GetOrdinal("ID")),
                            ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                            EqualizerPresets = !reader.IsDBNull(reader.GetOrdinal("EqualizerPresets")) ? reader.GetString(reader.GetOrdinal("EqualizerPresets")) : "{}",
                            LastVolume = reader.GetInt32(reader.GetOrdinal("LastVolume")),
                            Theme = !reader.IsDBNull(reader.GetOrdinal("Theme")) ? reader.GetString(reader.GetOrdinal("Theme")) : "{}",
                            DynamicPause = reader.GetBoolean(reader.GetOrdinal("DynamicPause")),
                            BlacklistDirectory = !reader.IsDBNull(reader.GetOrdinal("BlacklistDirectory")) ? (string[])reader.GetValue(reader.GetOrdinal("BlacklistDirectory")) : Array.Empty<string>(),
                            TrackSortingOrderState = !reader.IsDBNull(reader.GetOrdinal("TrackSortingOrderState")) ? reader.GetString(reader.GetOrdinal("TrackSortingOrderState")) : "name_asc",
                            LastPlayedTrackID = !reader.IsDBNull(reader.GetOrdinal("LastPlayedTrackID")) ? reader.GetInt32(reader.GetOrdinal("LastPlayedTrackID")) : null,
                            LastPlayedPosition = reader.GetInt32(reader.GetOrdinal("LastPlayedPosition")),
                            ShuffleEnabled = reader.GetBoolean(reader.GetOrdinal("ShuffleEnabled")),
                            RepeatMode = !reader.IsDBNull(reader.GetOrdinal("RepeatMode")) ? reader.GetString(reader.GetOrdinal("RepeatMode")) : "none",
                            LastQueueState = !reader.IsDBNull(reader.GetOrdinal("LastQueueState")) ? reader.GetString(reader.GetOrdinal("LastQueueState")) : "{}",
                            QueueState = !reader.IsDBNull(reader.GetOrdinal("QueueState")) ? reader.GetString(reader.GetOrdinal("QueueState")) : "{}",
                            ViewState = !reader.IsDBNull(reader.GetOrdinal("ViewState")) ? reader.GetString(reader.GetOrdinal("ViewState")) : "{\"tracks\": \"grid\"}",
                            SortingState = !reader.IsDBNull(reader.GetOrdinal("SortingState")) ? reader.GetString(reader.GetOrdinal("SortingState")) : "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
                        };
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching profile config: {ex.Message}");
                throw;
            }
        }

        public async Task<int> CreateProfileConfig(int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO ProfileConfig (ProfileID)
                        VALUES (@ProfileID)
                        RETURNING ID";

                    using var cmd = new NpgsqlCommand(query, db.dbConn);
                    cmd.Parameters.AddWithValue("ProfileID", profileId);

                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating profile config: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateProfileConfig(ProfileConfig config)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                UPDATE ProfileConfig SET 
                    EqualizerPresets = @EqualizerPresets,
                    LastVolume = @LastVolume,
                    Theme = @Theme,
                    DynamicPause = @DynamicPause,
                    BlacklistDirectory = @BlacklistDirectory,
                    TrackSortingOrderState = @TrackSortingOrderState,
                    LastPlayedTrackID = @LastPlayedTrackID,
                    LastPlayedPosition = @LastPlayedPosition,
                    ShuffleEnabled = @ShuffleEnabled,
                    RepeatMode = @RepeatMode,
                    LastQueueState = @LastQueueState,
                    QueueState = @QueueState,
                    ViewState = @ViewState,
                    SortingState = @SortingState
                WHERE ProfileID = @ProfileID";

                    using var cmd = new NpgsqlCommand(query, db.dbConn);
                    cmd.Parameters.AddWithValue("ProfileID", config.ProfileID);
                    cmd.Parameters.AddWithValue("EqualizerPresets", NpgsqlTypes.NpgsqlDbType.Jsonb, config.EqualizerPresets ?? "{}");
                    cmd.Parameters.AddWithValue("LastVolume", config.LastVolume);
                    cmd.Parameters.AddWithValue("Theme", NpgsqlTypes.NpgsqlDbType.Jsonb, config.Theme ?? "{}");
                    cmd.Parameters.AddWithValue("DynamicPause", config.DynamicPause);
                    cmd.Parameters.AddWithValue("BlacklistDirectory", config.BlacklistDirectory);
                    cmd.Parameters.AddWithValue("TrackSortingOrderState", config.TrackSortingOrderState);
                    cmd.Parameters.AddWithValue("LastPlayedTrackID", config.LastPlayedTrackID ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("LastPlayedPosition", config.LastPlayedPosition);
                    cmd.Parameters.AddWithValue("ShuffleEnabled", config.ShuffleEnabled);
                    cmd.Parameters.AddWithValue("RepeatMode", config.RepeatMode);
                    cmd.Parameters.AddWithValue("LastQueueState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.LastQueueState ?? "{}");
                    cmd.Parameters.AddWithValue("QueueState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.QueueState ?? "{}");
                    cmd.Parameters.AddWithValue("ViewState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.ViewState ?? "{}");
                    cmd.Parameters.AddWithValue("SortingState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.SortingState ?? "{}");

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating profile config: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteProfileConfig(int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM ProfileConfig WHERE ProfileID = @ProfileID";
                    using var cmd = new NpgsqlCommand(query, db.dbConn);
                    cmd.Parameters.AddWithValue("ProfileID", profileId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting profile config: {ex.Message}");
                throw;
            }
        }
    }
}
