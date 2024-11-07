using Npgsql;
using System;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class DbConnection : IDisposable
    {
        public NpgsqlConnection dbConn { get; }
        public DbConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            dbConn = new NpgsqlConnection(connectionString);
            dbConn.Open();
        }

        public void Dispose()
        {
            if (dbConn != null)
            {
                dbConn.Close();
                dbConn.Dispose();
            }
        }
    }
}
