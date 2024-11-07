using Avalonia.Controls;
using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class DirectoriesRepository
    {
        public async Task<Directories> GetDirectoryById(int dirID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Directories WHERE dirID = @dirID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("dirID", dirID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Directories
                                {
                                    DirID = reader.GetInt32(reader.GetOrdinal("dirID")),
                                    DirPath = reader.GetString(reader.GetOrdinal("dirPath"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the Directory by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Directories>> GetAllDirectories()
        {
            var directories = new List<Directories>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Directories";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var directory = new Directories
                                {
                                    DirID = reader.GetInt32(reader.GetOrdinal("dirID")),
                                    DirPath = reader.GetString(reader.GetOrdinal("dirPath"))
                                };

                                directories.Add(directory);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all Directories: {ex.Message}");
                throw;
            }

            return directories;
        }

        public async Task<int> AddDirectory(Directories directory)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Directories (dirPath)
                        VALUES (@dirPath) RETURNING dirID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("dirPath", directory.DirPath);

                        var dirID = (int)cmd.ExecuteScalar();
                        return dirID;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the Directory: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteDirectory(int dirID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Directories WHERE dirID = @dirID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("dirID", dirID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the Directory: {ex.Message}");
                throw;
            }
        }
    }
}
