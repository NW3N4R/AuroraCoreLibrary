
using AuroraCRUD.Attributes;

using System.Collections.ObjectModel;

using System.Reflection;

namespace AuroraCRUD.Services
{
    public static class StarterLoadup
    {
        public static async Task LoadDataAsync<T>()
        {
            var properties = typeof(T)
                .GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(TableIdentifier)))
                .ToList();

            // Dictionary to hold tasks keyed by property
            var tasks = new Dictionary<PropertyInfo, Task<object>>();

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<TableIdentifier>();

                var propType = prop.PropertyType;

                if (propType.IsGenericType &&
                    propType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
                {
                    // Get the inner type T
                    var innerType = propType.GetGenericArguments()[0];

                    // Call DataBaseService.GetDataAsync<T>(tableName) dynamically
                    var method = typeof(DataBaseService)?
                        .GetMethod("GetDataAsync")?
                        .MakeGenericMethod(innerType);

                    Task task = (Task)method?.Invoke(null, null);
                    await task;
                    // Convert Task<T> to Task<object> for awaiting later
                    var boxedTask = task?.ContinueWith(t => ((dynamic)t).Result as object);
                    tasks.Add(prop, boxedTask);
                }
            }

            // Await all tasks
            await Task.WhenAll(tasks.Values);

            // Set results to corresponding properties
            foreach (var kvp in tasks)
            {
                var prop = kvp.Key;
                var result = await kvp.Value; // This is List<T> from GetDataAsync<T>

                // Convert List<T> to ObservableCollection<T>
                var innerType = prop.PropertyType.GetGenericArguments()[0];
                var obsCollection = Activator.CreateInstance(typeof(ObservableCollection<>).MakeGenericType(innerType), result);

                // Set the ObservableCollection to the property
                prop.SetValue(this, obsCollection);
            }
        }
    }
}
