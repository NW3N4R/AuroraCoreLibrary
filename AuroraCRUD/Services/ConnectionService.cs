using Microsoft.Data.SqlClient;

namespace AuroraCRUD.Services
{
    public static class ConnectionService
    {
        public static SqlConnection? sqlConnection;
        public static async Task OpenDatabaseConnection()
        {
            Logger.Log("opening connection");
            if (sqlConnection == null)
            {
                Logger.Log("couldn't open connection since it was null", logStatus.error);
                return;
            }
            while (sqlConnection.State != System.Data.ConnectionState.Open)
            {
                if (sqlConnection.State == System.Data.ConnectionState.Connecting)
                {
                    Logger.Log("trying to open the connection", logStatus.ongoing);
                    continue;
                } else if (sqlConnection.State == System.Data.ConnectionState.Open)
                {
                    Logger.Log("connection is open", logStatus.success);
                    break;
                }
                await sqlConnection.OpenAsync();
            }
        }

        public static async Task CloseDatabaseConnection()
        {
            Logger.Log("Closing connection");
            if (sqlConnection != null)
            {
                Logger.Log("connection must be closed now");
                await sqlConnection.CloseAsync();
            }
        }
    }
}
