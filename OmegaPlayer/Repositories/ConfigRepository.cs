using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class ConfigRepository
    {
        public async Task<Config> GetConfigById(int configID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Config WHERE configID = @configID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("configID", configID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Config
                                {
                                    ConfigID = reader.GetInt32(reader.GetOrdinal("configID")),
                                    Styles = reader.GetString(reader.GetOrdinal("styles")),
                                    ColorTheme = reader.GetString(reader.GetOrdinal("colortheme")),
                                    MainColor = reader.GetString(reader.GetOrdinal("maincolor")),
                                    SideBarPosition = reader.GetString(reader.GetOrdinal("sidebarposition")),

                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the config: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Config>> GetAllConfigs()
        {
            var configs = new List<Config>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Config";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var config = new Config
                                {
                                    ConfigID = reader.GetInt32(reader.GetOrdinal("configID")),
                                    Styles = reader.GetString(reader.GetOrdinal("styles")),
                                    ColorTheme = reader.GetString(reader.GetOrdinal("colortheme")),
                                    MainColor = reader.GetString(reader.GetOrdinal("maincolor")),
                                    SideBarPosition = reader.GetString(reader.GetOrdinal("sidebarposition")),
                                };

                                configs.Add(config);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all configs: {ex.Message}");
                throw;
            }

            return configs;
        }

        public async Task<int> AddConfig(Config config)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Config (styles, colortheme, maincolor, sidebarposition)
                        VALUES (@styles, @colortheme, @maincolor, @sidebarposition) RETURNING configID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("styles", config.Styles);
                        cmd.Parameters.AddWithValue("colortheme", config.ColorTheme);
                        cmd.Parameters.AddWithValue("maincolor", config.MainColor);
                        cmd.Parameters.AddWithValue("sidebarposition", config.SideBarPosition);

                        var configID = (int)cmd.ExecuteScalar();
                        return configID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the config: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateConfig(Config config)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Config SET 
                            styles = @styles,
                            colortheme = @colortheme,
                            maincolor = @maincolor,
                            sidebarposition = @sidebarposition,
                        WHERE configID = @configID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("configID", config.ConfigID);
                        cmd.Parameters.AddWithValue("styles", config.Styles);
                        cmd.Parameters.AddWithValue("colortheme", config.ColorTheme);
                        cmd.Parameters.AddWithValue("maincolor", config.MainColor);
                        cmd.Parameters.AddWithValue("sidebarposition", config.SideBarPosition);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the config: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteConfig(int configID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Config WHERE configID = @configID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("configID", configID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the config: {ex.Message}");
                throw;
            }
        }
    }
}
