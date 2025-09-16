using AuroraCRUD.Attributes;
using AuroraCRUD.services;
using AuroraCRUD.Services.ModelService;

using Microsoft.Data.SqlClient;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

namespace AuroraCRUD.Services
{
    public class DataBaseListener<T> where T : class
    {
        private readonly List<QueryNotifier> TablesListenerList = new();
        private readonly ConcurrentQueue<ChangeTrackingModel> Queue = new();
        private readonly Dictionary<string, SemaphoreSlim> TableLocks = new();
        private readonly CancellationTokenSource cts = new();
        private bool isRunning = false;
        private ObservableCollection<T> baseCollection;
        public DataBaseListener(SqlConnection _connection, ObservableCollection<T> _baseCollection, string nameSpaceString)
        {
            foreach (var item in TableInfos(nameSpaceString))
            {
                string[] columns = item.columns.ToArray();
                TablesListenerList.Add(new QueryNotifier(_connection, item.tableName, columns));
            }
            this.baseCollection = _baseCollection;
        }

        public async void StartListening()
        {
            if (isRunning)
                return;
            isRunning = true;

            foreach (var item in TablesListenerList)
            {
                await item.InitializeChangeTracking();
                await item.StartListening();
                item.Changed += Item_Changed;
            }

            _ = Task.Run(ProcessTheChangeLoop, cts.Token);
        }

        private void Item_Changed(object? sender, ChangeTrackingModel e)
        {
            Queue.Enqueue(e);
            Debug.WriteLine(e.TableName + "  " + e.Id + "  " + e.Operation);
        }

        private async Task ProcessTheChangeLoop()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (Queue.TryDequeue(out var ctModel))
                {
                    try
                    {
                        await ProcessChange(ctModel);
                    } catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing change for table {ctModel.TableName}: {ex.Message}");
                    }
                } else
                {
                    await Task.Delay(50, cts.Token); // prevent tight loop
                }
            }
        }

        private async Task ProcessChange(ChangeTrackingModel ctModel)
        {
            if (!TableLocks.ContainsKey(ctModel.TableName))
                TableLocks[ctModel.TableName] = new SemaphoreSlim(1, 1);

            await TableLocks[ctModel.TableName].WaitAsync();
            try
            {
                switch (ctModel.Operation)
                {
                    case "I": // Insert
                        await HandleInsertOperation(ctModel, typeof(T));
                        break;
                    case "U": // Update
                        await HandleUpdateOperation(ctModel, typeof(T));
                        break;
                    case "D": // Delete
                        HandleDeleteOperation(ctModel);
                        break;
                }
            } finally
            {
                TableLocks[ctModel.TableName].Release();
            }
        }

        private async Task HandleInsertOperation(ChangeTrackingModel ctModel, Type modelType)
        {
            try
            {
                dynamic? dynamicTask = await getTheItem(ctModel, modelType);
                if (dynamicTask == null)
                {
                    return;
                }
                var insertedItem = dynamicTask.Result;

                if (insertedItem != null)
                {
                    baseCollection.Insert(0, insertedItem);
                }
            } catch
            {

            }
        }

        private async Task HandleUpdateOperation(ChangeTrackingModel ctModel, Type modelType)
        {
            try
            {
                dynamic? dynamicTask = await getTheItem(ctModel, modelType);
                if (dynamicTask == null)
                {
                    return;
                }
                var updatedItem = dynamicTask.Result;
                if (updatedItem != null)
                {
                    var existing = baseCollection.FirstOrDefault(x =>
                     x != null &&
                    (int?)x.GetType()?.GetProperty("id")?.GetValue(x) == ctModel.Id);

                    if (existing != null)
                    {
                        int index = baseCollection.IndexOf(existing);
                        baseCollection.Remove(existing);
                        baseCollection.Insert(index, updatedItem);
                    }
                }
            } catch
            {
            }
        }

        private async Task<dynamic?> getTheItem(ChangeTrackingModel ctModel, Type modelType)
        {
            if (ctModel == null || modelType == null)
                return null;
            var method = typeof(DataBaseService)?.GetMethod("GetSingleRowAsync");
            if (method == null)
                return null;

            var genericMethod = method.MakeGenericMethod(modelType);
            var task = genericMethod.Invoke(null, new object[] { ctModel.TableName, ctModel.Id }) as Task;

            if (task == null)
                return null;

            await task.ConfigureAwait(false);

            // Use dynamic but with null checks
            dynamic dynamicTask = task;
            return dynamicTask;
        }

        private void HandleDeleteOperation(ChangeTrackingModel ctModel)
        {
            var existing = baseCollection.FirstOrDefault(x =>
                 x != null &&
                (int?)x.GetType()?.GetProperty("id")?.GetValue(x) == ctModel.Id);
            if (existing != null)
            {
                baseCollection.Remove(existing);
            }
        }

        public static List<(string tableName, List<string> columns)> TableInfos(string nameSpaceString)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var modelProperties = new List<(string, List<string>)>();

            var types = assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace == nameSpaceString);
            // "AuroraMarketDesktop.Models" is name space of the models

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<TableIdentifier>();
                var tableName = attr?.Name ?? type.Name;

                var properties = type.GetProperties()
                    .Where(p => p.CanRead &&
                                p.GetMethod?.IsPublic == true &&
                                !Attribute.IsDefined(p, typeof(NotColumn)))
                    .Select(p => p.Name)
                    .ToList();

                modelProperties.Add((tableName, properties));
            }

            return modelProperties;
        }

        public void StopListening()
        {
            cts.Cancel();
            isRunning = false;
        }
    }
}