using AuroraCRUD.Services.ModelService;

using Microsoft.Data.SqlClient;

using System.Data;
using System.Diagnostics;

using static AuroraCRUD.Services.ConnectionService;
namespace AuroraCRUD.services
{
    public class QueryNotifier
    {
        bool _isListening = false;
        internal readonly string TableName;
        private readonly string[] Columns;
        private long _lastChangeVersion = 0;
        public event EventHandler<ChangeTrackingModel>? Changed;
        internal readonly Type type;
        public QueryNotifier(string _tableName, string[] columns, Type _type)
        {
            this.TableName = _tableName;
            this.Columns = columns;
            this.type = _type;
            if (sqlConnection != null)
                SqlDependency.Start(sqlConnection.ConnectionString);
        }

        public async Task InitializeChangeTracking()
        {
            try
            {

                // Get the current change version to start from
                string query = @"
DECLARE @CurrentVersion bigint = CHANGE_TRACKING_CURRENT_VERSION();
SELECT ISNULL(MAX(SYS_CHANGE_VERSION), 0) AS LastChangeVersion 
FROM CHANGETABLE(CHANGES dbo.{TableName}, 0) AS CT;";

                query = query.Replace("{TableName}", TableName);

                using var command = new SqlCommand(query, sqlConnection);
                var result = await command.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    _lastChangeVersion = Convert.ToInt64(result);
                    Debug.WriteLine($"Change tracking initialized for {TableName}. Last version: {_lastChangeVersion}");
                } else
                {
                    _lastChangeVersion = 0;
                    Debug.WriteLine($"Change tracking initialized for {TableName}. Starting from version 0");
                }
            } catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing change tracking for {TableName}: {ex.Message}");
                _lastChangeVersion = 0;
            }
        }

        public async Task StartListening()
        {
            if (_isListening)
                return;

            string query = $@" SELECT 
                               {string.Join(",", Columns)}
                               FROM dbo.{TableName}";
            try
            {


                SqlCommand _command = new SqlCommand(query, sqlConnection);
                _command.Notification = null;

                SqlDependency _dependency = new SqlDependency(_command);
                _dependency.OnChange += OnDependencyOnChange;

                await _command.ExecuteNonQueryAsync();
                _isListening = true;
                Debug.WriteLine($"Started listening for changes on table {TableName}... ");
            } catch (Exception ex)
            {
                Debug.WriteLine($"Error starting listener on table {TableName} error is : {ex.Message}");
                _isListening = false;
            }
        }

        private async void OnDependencyOnChange(object sender, SqlNotificationEventArgs e)
        {
            var dependency = (SqlDependency)sender;
            dependency.OnChange -= OnDependencyOnChange;
            _isListening = false;

            if (e.Type == SqlNotificationType.Change)
            {
                await GetChanges();
                await StartListening(); // Restart listening
            } else
            {
                Debug.WriteLine($"Unexpected notification: Type={e.Type}, Info={e.Info}");
                await StartListening(); // Restart listening even on unexpected notifications
            }
        }

        public async Task GetChanges()
        {
            try
            {

                string query = @"
SELECT 
    CT.SYS_CHANGE_VERSION AS ChangeVersion,
    CT.SYS_CHANGE_OPERATION AS Operation,
    CT.id AS Id
FROM CHANGETABLE(CHANGES dbo.{TableName}, @lastChangeVersion) AS CT
LEFT JOIN dbo.{TableName} AS T ON CT.id = T.id
ORDER BY CT.SYS_CHANGE_VERSION ASC";

                query = query.Replace("{TableName}", TableName);

                using var command = new SqlCommand(query, sqlConnection);
                command.Parameters.Add("@lastChangeVersion", SqlDbType.BigInt).Value = _lastChangeVersion;

                using var reader = await command.ExecuteReaderAsync();

                int changeCount = 0;

                while (await reader.ReadAsync())
                {
                    long changeVersion = reader.GetInt64(0);
                    string operation = reader.GetString(1);
                    int id = reader.GetInt32(2);

                    var changeModel = new ChangeTrackingModel
                    {
                        ChangeVersion = changeVersion,
                        Operation = operation,
                        Id = id,
                        TableName = TableName,
                        modelType = this.type
                    };

                    Changed?.Invoke(null, changeModel);

                    if (changeVersion > _lastChangeVersion)
                    {
                        _lastChangeVersion = changeVersion;
                    }

                    changeCount++;

                }

            } catch (Exception ex)
            {
                Debug.WriteLine($"Error getting changes from {TableName}: {ex.Message}");
            }
        }

    }
}