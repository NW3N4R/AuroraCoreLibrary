using AuroraCRUD.Attributes;

using Dapper;

using Microsoft.Data.SqlClient;

using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;

using static Aurora.Reflections.CoreReflections;
using static AuroraCRUD.Services.ConnectionService;
namespace AuroraCRUD.Services
{
    public class DataBaseService
    {
        public static List<string> Tables = new();

        public static async Task<ObservableCollection<T>?> GetDataAsync<T>() where T : class, new()
        {
            if (sqlConnection == null)
                return null;
            ObservableCollection<T> result = new ObservableCollection<T>();
            string query = $"SELECT * FROM {GetTableNameFromModel<T>()}";

            try
            {
                Logger.Log($"Executing query: {query}", logStatus.ongoing);


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
                                prop.SetValue(obj, "-");
                            continue;
                        }

                        object value;
                        if (prop.PropertyType == typeof(DateOnly) && rawValue is DateTime dateTimeValue)
                            value = DateOnly.FromDateTime(dateTimeValue);
                        else
                            value = Convert.ChangeType(rawValue, prop.PropertyType);

                        prop.SetValue(obj, value);
                    }

                    result.Add(obj);
                }

                Logger.Log($"Query completed successfully, {result.Count} rows loaded.", logStatus.success);
            } catch (Exception ex)
            {
                Logger.Log($"Error in GetDataAsync<{typeof(T).Name}>: {ex.Message}", logStatus.error);
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
            if (sqlConnection == null)
                return 0;
            try
            {
                Logger.Log("Inserting data", logStatus.ongoing);
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
                Logger.Log($"generated query: {query}", logStatus.ongoing);
                var parameters = new DynamicParameters();
                foreach (var prop in properties)
                {
                    if (excluded.Any(attrType => Attribute.IsDefined(prop, attrType)))
                        continue;

                    object value = prop.GetValue(data) ?? DBNull.Value;
                    if (value is DateOnly dateOnly)
                        value = dateOnly.ToDateTime(TimeOnly.MinValue);
                    Logger.Log($"generated parameters @{prop.Name} with value {value}", logStatus.ongoing);
                    parameters.Add("@" + prop.Name, value);
                }

                // Execute and get ID
                object? idObj = await sqlConnection.ExecuteScalarAsync(query, parameters);
                int newId = (idObj == null) ? 0 : Convert.ToInt32(idObj);
                Logger.Log($"executed query with result {newId}", newId == 0 ? logStatus.error : logStatus.success);

                return newId;
            } catch (Exception ex)
            {
                Logger.Log($"inserting failed: {ex.Message}", logStatus.error);
                return 0;
            }
        }

        public static async Task<bool> UpdateDataAsync<T>(T data) where T : class
        {
            try
            {
                Logger.Log("updating...", logStatus.ongoing);
                PropertyInfo[] properties = typeof(T).GetProperties();
                var pkProperty = GetPrimaryKeys<T>().First();
                string? keyColumn = pkProperty?.Name;
                object? keyValue = pkProperty?.GetValue(data)!;
                string[] setClauses = properties
                    .Where(p => p.Name != keyColumn && !Attribute.IsDefined(p, typeof(PrimaryKey))
                    && !Attribute.IsDefined(p, typeof(NotColumn)))
                    .Select(p => $"{p.Name} = @{p.Name}")
                    .ToArray();

                // Append the condition to the array of properties -- this will add where id= @id at the last index 
                setClauses = setClauses.Append($"where {keyColumn} = @{keyColumn}").ToArray();

                // Build SQL Update Query
                string query = $"UPDATE {GetTableNameFromModel<T>()} SET {string.Join(", ", setClauses)} ";
                Logger.Log($"generated query {query}", logStatus.ongoing);

                // before the 'where' we have a comma (,) we need to remove it
                int index = query.LastIndexOf(',');
                if (index != -1)
                {
                    query = query.Remove(index, 1);
                    Logger.Log($"fixed query {query}", logStatus.ongoing);
                }

                // Create a dictionary of parameters
                var parameters = new DynamicParameters();

                SqlCommand cmd = new SqlCommand(query, sqlConnection);

                foreach (var prop in properties.Where(p => !Attribute.IsDefined(p, typeof(NotColumn))))
                {
                    object value = prop.GetValue(data) ?? DBNull.Value;
                    Logger.Log($"initiated parameters @{prop.Name} with value {value}", logStatus.ongoing);
                    cmd.Parameters.AddWithValue("@" + prop.Name, value);
                }

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                Logger.Log($"executed procedure with result {rowsAffected} as rf", rowsAffected > 0 ? logStatus.success : logStatus.error);
                return rowsAffected > 0;
            } catch (Exception ex)
            {
                Logger.Log($"exception occurred {ex.Message}", logStatus.error);
                return false;
            }
        }

        public static async Task<bool> DeleteDataAsync<T>(T data) where T : class
        {
            Logger.Log($"executing DeleteDataAsync", logStatus.ongoing);
            if (sqlConnection == null)
                throw new Exception("sql connection was null here");

            try
            {
                var pkProperty = GetPrimaryKeys<T>().FirstOrDefault();

                if (pkProperty == null)
                    throw new InvalidOperationException($"No [PrimaryKey] attribute found on {typeof(T).Name}");

                string keyColumn = pkProperty.Name;
                object keyValue = pkProperty.GetValue(data)!;
                Logger.Log($"Found primary key and its value {keyColumn} {keyValue}", logStatus.ongoing);

                // Build SQL Delete Query
                string query = $"DELETE FROM {GetTableNameFromModel<T>()} WHERE {keyColumn} = @{keyColumn}";
                Logger.Log($"Generated Query {query}", logStatus.ongoing);

                // Execute Delete Query
                var parameters = new DynamicParameters();
                parameters.Add("@" + keyColumn, keyValue);
                Logger.Log($"Setup  Parameters @{keyColumn} {keyValue}", logStatus.ongoing);

                int rowsAffected = await sqlConnection.ExecuteAsync(query, parameters);
                Logger.Log($"Executed Query result is {rowsAffected} as rf", rowsAffected > 0 ? logStatus.success : logStatus.error);

                return rowsAffected > 0;
            } catch (Exception ex)
            {
                Logger.Log($"Exception occured {ex.Message}", logStatus.error);

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

        public static async Task<bool> ExecuteObjectAndList<TList, TInvoice>(string procedureName, object model, List<TList> list, string listName, List<Type> exluded) where TList : class where TInvoice : class
        {
            Logger.Log("executing object and list");
            if (sqlConnection == null)
                throw new Exception("sql connection was null here");

            var parameters = new DynamicParameters();

            var modelProperties = GetPropertiesInfo<TInvoice>(exluded);

            foreach (var prop in modelProperties)
            {
                object val = prop.GetValue(model) ?? DBNull.Value;
                if (val is DateOnly dtOnly)
                    val = dtOnly.ToDateTime(TimeOnly.MinValue);

                parameters.Add("@" + prop.Name, val);
            }

            var dataTable = ListToDataTable(list, new List<Type> { typeof(PrimaryKey), typeof(NotColumn) });
            parameters.Add($"@{listName}", dataTable.AsTableValuedParameter("dbo.soldItems")); // Match your TVP name

            int rf = await sqlConnection.QuerySingleAsync<int>(procedureName, parameters, commandType: CommandType.StoredProcedure);
            Logger.Log($"executed procedure {procedureName} returned {rf} as rf", rf > 0 ? logStatus.success : logStatus.error);
            return rf > 0;
        }

    }
}