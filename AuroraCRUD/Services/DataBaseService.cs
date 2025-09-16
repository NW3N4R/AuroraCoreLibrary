using AuroraCRUD.Attributes;

using Dapper;

using Microsoft.Data.SqlClient;

using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;

using static AuroraCRUD.CoreReflections;
namespace AuroraCRUD.Services
{
    public class DataBaseService
    {
        public static SqlConnection? sqlConnection;

        public static List<string> Tables = new();

        public static async Task OpenDatabaseConnection()
        {
            if (sqlConnection == null)
            {
                throw new Exception("db constr was null");
            }
            while (sqlConnection.State != System.Data.ConnectionState.Open)
            {
                if (sqlConnection.State == System.Data.ConnectionState.Connecting)
                {
                    continue;
                } else if (sqlConnection.State == System.Data.ConnectionState.Open)
                {
                    break;
                }
                await sqlConnection.OpenAsync();
            }
        }
        public static async Task CloseDatabaseConnection()
        {
            if (sqlConnection != null)
            {
                await sqlConnection.CloseAsync();
            }
        }
        public static async Task<ObservableCollection<T>> GetDataAsync<T>() where T : class, new()
        {
            ObservableCollection<T> result = new ObservableCollection<T>();

            string query = $"SELECT * FROM {GetTableNameFromModel<T>()}";

            var rows = await sqlConnection.QueryAsync(query);

            PropertyInfo[] properties = typeof(T).GetProperties();

            foreach (var row in rows)
            {
                T obj = new T();
                var rowDictionary = (IDictionary<string, object>)row;

                foreach (var prop in properties)
                {
                    object rawValue = rowDictionary[prop.Name];

                    if (rawValue == null || rawValue == DBNull.Value)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                        {
                            prop.SetValue(obj, "-");
                        }
                        continue;
                    }

                    object value;
                    if (prop.PropertyType == typeof(DateOnly) && rawValue is DateTime dateTimeValue)
                    {
                        value = DateOnly.FromDateTime(dateTimeValue);
                    } else
                    {
                        value = Convert.ChangeType(rawValue, prop.PropertyType);
                    }

                    prop.SetValue(obj, value);
                }

                result.Add(obj);
            }

            return result;
        }

        public static async Task<T?> GetSingleRowAsync<T>(string tableName, int id) where T : class, new()
        {
            //this method will be called/execued at runtime
            string query = $"SELECT top(1) * FROM {tableName} where id = {id}";
            if (sqlConnection == null)
                return null;

            var rows = await sqlConnection.QueryAsync(query);

            PropertyInfo[] properties = typeof(T).GetProperties()
                .Where(x => !Attribute.IsDefined(x, typeof(NotColumn))).ToArray();
            foreach (var row in rows)
            {
                T obj = new T();
                var rowDictionary = (IDictionary<string, object>)row;

                foreach (var prop in properties)
                {
                    object rawValue = rowDictionary[prop.Name];

                    if (rawValue == null || rawValue == DBNull.Value)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                        {
                            prop.SetValue(obj, "-");
                        }
                        continue;
                    }

                    object value;
                    if (prop.PropertyType == typeof(DateOnly) && rawValue is DateTime dateTimeValue)
                    {
                        value = DateOnly.FromDateTime(dateTimeValue);
                    } else
                    {
                        value = Convert.ChangeType(rawValue, prop.PropertyType);
                    }

                    prop.SetValue(obj, value);
                }
                return obj;
            }

            return null;
        }

        public static async Task<int> InsertDataAsync<T>(T data) where T : class
        {
            try
            {
                List<Type> excluded = new List<Type>()
                {
                    typeof(PrimaryKey),
                    typeof(NotColumn)
                };
                PropertyInfo[] properties = typeof(T).GetProperties();
                string[] columnNames = GetPropertiesName<T>(excluded);
                string[] paramNames = GetSQLParameters<T>(excluded);

                string query = $"INSERT INTO {GetTableNameFromModel<T>()} ({string.Join(", ", columnNames)}) " +
                               $"VALUES ({string.Join(", ", paramNames)}); SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var parameters = new DynamicParameters();
                foreach (var prop in properties)
                {
                    if (Attribute.IsDefined(prop, typeof(PrimaryKey)))
                        continue;

                    object value = prop.GetValue(data) ?? DBNull.Value;
                    if (value is DateOnly dateOnly)
                        value = dateOnly.ToDateTime(TimeOnly.MinValue);

                    parameters.Add("@" + prop.Name, value);
                }

                // Execute and get ID
                object? idObj = await sqlConnection.ExecuteScalarAsync(query, parameters);
                int newId = (idObj == null) ? 0 : Convert.ToInt32(idObj);


                return newId;
            } catch (Exception ex)
            {
                return 0;
            }
        }

        public static async Task<bool> UpdateDataAsync<T>(T data) where T : class
        {
            try
            {
                PropertyInfo[] properties = typeof(T).GetProperties();
                var pkProperty = GetPrimaryKey<T>().First();
                string? keyColumn = pkProperty?.Name;
                object? keyValue = pkProperty?.GetValue(data)!;
                string[] setClauses = properties
                    .Where(p => p.Name != keyColumn && !Attribute.IsDefined(p, typeof(PrimaryKey)))
                    .Select(p => $"{p.Name} = @{p.Name}")
                    .ToArray();

                // Append the condition to the array of properties -- this will add where id= @id at the last index 
                setClauses = setClauses.Append($"where {keyColumn} = @{keyColumn}").ToArray();

                // Build SQL Update Query
                string query = $"UPDATE {GetTableNameFromModel<T>()} SET {string.Join(", ", setClauses)} ";

                // before the 'where' we have a comma (,) we need to remove it
                int index = query.LastIndexOf(',');
                if (index != -1)
                {
                    query = query.Remove(index, 1);
                }

                // Create a dictionary of parameters
                var parameters = new DynamicParameters();

                SqlCommand cmd = new SqlCommand(query, sqlConnection);

                foreach (var prop in properties.Where(p => !Attribute.IsDefined(p, typeof(PrimaryKey))))
                {
                    object value = prop.GetValue(data) ?? DBNull.Value;
                    cmd.Parameters.AddWithValue("@" + prop.Name, value);
                }

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                return rowsAffected > 0;
            } catch
            {
                return false;
            }
        }

        public static async Task<bool> DeleteDataAsync<T>(T data) where T : class
        {
            if (sqlConnection == null)
                throw new Exception("sql connection was null here");

            try
            {
                var pkProperty = GetPrimaryKey<T>().FirstOrDefault();

                if (pkProperty == null)
                    throw new InvalidOperationException($"No [PrimaryKey] attribute found on {typeof(T).Name}");

                string keyColumn = pkProperty.Name;
                object keyValue = pkProperty.GetValue(data)!;

                // Build SQL Delete Query
                string query = $"DELETE FROM {GetTableNameFromModel<T>()} WHERE {keyColumn} = @{keyColumn}";

                // Execute Delete Query
                var parameters = new DynamicParameters();
                parameters.Add("@" + keyColumn, keyValue);

                int rowsAffected = await sqlConnection.ExecuteAsync(query, parameters);

                return rowsAffected > 0;
            } catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> ExecuteStoredProcedure(string procedureName, Dictionary<string, object> parameters)
        {
            try
            {
                using (SqlCommand command = new SqlCommand(procedureName, sqlConnection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Add parameters dynamically to the command
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }

                    // Execute the command and get the SCOPE_IDENTITY() if needed (useful for insert operations)
                    int result = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return result;
                }
            } catch (Exception)
            {
                return 0;
            }
        }

        public static async Task<bool> ExecuteObjectAndList<T>(string procedureName, object model, List<T> list, string listName, bool CountPk = false) where T : class
        {
            if (sqlConnection == null)
                throw new Exception("sql connection was null here");

            var parameters = new DynamicParameters();

            // Add model (invoice) parameters
            var modelProperties = model.GetType().GetProperties();
            foreach (var prop in modelProperties)
            {
                bool isPk = Attribute.IsDefined(prop, typeof(PrimaryKey));
                bool isIgnored = Attribute.IsDefined(prop, typeof(PrimaryKey));

                // Include PK only if CountPk==true, ignore IgnoreInsert for PK in that case
                if (isPk && CountPk)
                {
                    object value = prop.GetValue(model) ?? DBNull.Value;

                    parameters.Add("@" + prop.Name, value);
                    if (value == DBNull.Value)
                    {
                        throw new Exception("null");
                    }
                    continue;  // Move to next property
                }
                // Skip if ignored and not a PK included above
                if (isIgnored)
                    continue;

                // Normal property handling
                object val = prop.GetValue(model) ?? DBNull.Value;
                if (val is DateOnly dtOnly)
                    val = dtOnly.ToDateTime(TimeOnly.MinValue);

                parameters.Add("@" + prop.Name, val);
            }

            // Add table-valued parameter
            var dataTable = ListToDataTable(list, new List<Type> { typeof(PrimaryKey) });
            parameters.Add($"@{listName}", dataTable.AsTableValuedParameter("dbo.soldItems")); // Match your TVP name

            await sqlConnection.ExecuteAsync(procedureName, parameters, commandType: CommandType.StoredProcedure);
            return true;
        }

    }
}