
using AuroraCRUD.services;
using AuroraCRUD.Services.ModelService;

using System.Collections.Concurrent;

using static Aurora.Reflections.CoreReflections;
namespace AuroraCRUD.Services
{
    public class DataBaseListener
    {
        public event EventHandler<(ChangeTrackingModel TrackingModel, dynamic? ChangedRow)>? TableChanged;
        private readonly List<QueryNotifier> TablesListenerList = new();
        private readonly ConcurrentQueue<ChangeTrackingModel> Queue = new();
        private readonly Dictionary<string, SemaphoreSlim> TableLocks = new();
        private readonly CancellationTokenSource cts = new();
        private bool isRunning = false;
        public void EnqueueTable(Type modelType)
        {
            foreach (var item in TableInfo(modelType))
            {
                string[] columns = item.columns.ToArray();
                TablesListenerList.Add(new QueryNotifier(item.tableName, columns, modelType));
            }
        }

        public async void StartListening()
        {
            Logger.Log($"Listenning executed -- ");
            if (isRunning)
            {
                Logger.Log($"listener was already running returning...");
                return;
            }
            isRunning = true;

            foreach (var item in TablesListenerList)
            {
                Logger.Log($"initiating Lisenter for {item.TableName}");
                await item.InitializeChangeTracking();
                Logger.Log($"Starting Listening");
                await item.StartListening();
                item.Changed += Item_Changed;
            }

            _ = Task.Run(ProcessTheChangeLoop, cts.Token);
        }

        private void Item_Changed(object? sender, ChangeTrackingModel e)
        {
            Logger.Log($"item changed {e.TableName} {e.Id}");
            Queue.Enqueue(e);
        }

        private async Task ProcessTheChangeLoop()
        {
            Logger.Log($"processing the change loop");
            while (!cts.Token.IsCancellationRequested)
            {
                if (Queue.TryDequeue(out var ctModel))
                {
                    try
                    {
                        await ProcessChange(ctModel);
                    } catch (Exception ex)
                    {
                        Logger.Log($"Error processing change for table {ctModel.TableName}: {ex.Message}", logStatus.error);
                    }
                } else
                {
                    await Task.Delay(50, cts.Token);
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
                Logger.Log($"processing changes for {ctModel.TableName}");
                var dyn = await GetTheChangedItem(ctModel);

                TableChanged?.Invoke(this, (ctModel, dyn));

            } finally
            {
                TableLocks[ctModel.TableName].Release();
            }
        }

        private async Task<object?> GetTheChangedItem(ChangeTrackingModel ctModel)
        {
            Logger.Log($"getting the changed Items");
            Type modelType = ctModel.modelType;
            if (ctModel == null || modelType == null)
            {
                Logger.Log($"either the ctModel or Model Type was null", logStatus.error);
                return null;
            }

            try
            {
                var method = typeof(DataBaseService).GetMethod("GetSingleRowAsync");
                if (method == null)
                {
                    Logger.Log($"the method \"GetSingleRowAsync\" was null", logStatus.error);
                    return null;
                }

                var genericMethod = method.MakeGenericMethod(modelType);

                var task = (Task)genericMethod.Invoke(null, new object[] { ctModel.TableName, ctModel.Id });

                if (task == null)
                {
                    Logger.Log($"task was null", logStatus.error);
                    return null;
                }

                await task.ConfigureAwait(false);

                // Get the result safely
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty?.GetValue(task);
            } catch (Exception ex)
            {
                Logger.Log($"Error getting changed item: {ex.Message}", logStatus.error);
                return null;
            }
        }

        public void StopListening()
        {
            cts.Cancel();
            isRunning = false;
        }
    }
}