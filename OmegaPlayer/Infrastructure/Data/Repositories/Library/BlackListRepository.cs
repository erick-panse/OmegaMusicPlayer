using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class BlackListRepository
    {
        public async Task<BlackList> GetBlackListById(int blackListID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM BlackList WHERE blackListID = @blackListID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("blackListID", blackListID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new BlackList
                                {
                                    BlackListID = reader.GetInt32(reader.GetOrdinal("blackListID")),
                                    BPath = reader.GetString(reader.GetOrdinal("bPath"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the BlackList by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<BlackList>> GetAllBlackLists()
        {
            var blackLists = new List<BlackList>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM BlackList";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var blackList = new BlackList
                                {
                                    BlackListID = reader.GetInt32(reader.GetOrdinal("blackListID")),
                                    BPath = reader.GetString(reader.GetOrdinal("bPath"))
                                };

                                blackLists.Add(blackList);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all BlackLists: {ex.Message}");
                throw;
            }

            return blackLists;
        }

        public async Task<int> AddBlackList(BlackList blackList)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO BlackList (bPath)
                        VALUES (@bPath) RETURNING blackListID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("bPath", blackList.BPath);

                        var blacklistID = (int)cmd.ExecuteScalar();
                        return blacklistID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the BlackList: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteBlackList(int blackListID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM BlackList WHERE blackListID = @blackListID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("blackListID", blackListID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the BlackList: {ex.Message}");
                throw;
            }
        }
    }
}
