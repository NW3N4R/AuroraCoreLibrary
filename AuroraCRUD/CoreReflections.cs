using AuroraCRUD.Attributes;

using System.Data;
using System.Reflection;

namespace Aurora.Reflections
{
    public static class CoreReflections
    {
        public static (object? collectionInstance, Type? modelType) GetObservableCollection(object tempStore, string name)
        {
            var property = tempStore.GetType()
                .GetProperties()
                .FirstOrDefault(p => Attribute.IsDefined(p, typeof(TableIdentifier)) &&
                       p.GetCustomAttribute<TableIdentifier>()?.Name == name);

            if (property != null)
            {
                var collectionInstance = property.GetValue(tempStore);
                var collectionType = property.PropertyType;
                var innerType = collectionType.GetGenericArguments()[0];
                return (collectionInstance, innerType);
            }

            return (null, null);
        }

        public static PropertyInfo[] GetPropertiesInfo<T>(List<Type> excludedAttributes)
        {

            var properties = typeof(T).GetProperties()
                .Where(x => !excludedAttributes
                .Any(a => Attribute.IsDefined(x, a)))
                .ToArray();

            return properties;
        }

        public static string[] GetPropertiesName<T>(List<Type> excludedAttributes)
        {
            var properties = typeof(T).GetProperties()
                .Where(x => !excludedAttributes
                .Any(a => Attribute.IsDefined(x, a)))
                .Select(x => x.Name)
                .ToArray();

            return properties;
        }

        public static string[] GetSQLParameters<T>(List<Type> excludedAttributes)
        {
            var properties = typeof(T).GetProperties()
                .Where(x => !excludedAttributes
                .Any(a => Attribute.IsDefined(x, a)))
                .Select(x => "@" + x.Name)
                .ToArray();

            return properties;
        }

        public static PropertyInfo[] GetPrimaryKeys<T>()
        {
            return typeof(T).GetProperties().Where(x => Attribute.IsDefined(x, typeof(PrimaryKey))).ToArray();
        }

        public static PropertyInfo[] GetForeinKeys<T>()
        {
            return typeof(T).GetProperties().Where(x => Attribute.IsDefined(x, typeof(ForeignKey))).ToArray();
        }

        public static string? GetTableNameFromModel<T>()
        {
            var tableAttr = typeof(T).GetCustomAttribute<TableIdentifier>();
            if (tableAttr == null)
            {
                Logger.Log($"Type {typeof(T).Name} does not have a TableIdentifier attribute. Looking For Table Name From Model", logStatus.error);
                return null;
            }

            return tableAttr.Name;
        }

        public static List<string> GetAllTableNames(string nameSpace)
        {
            // Get all loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && t.Namespace == nameSpace);

                    if (types.Any())
                    {
                        var modelProperties = new List<string>();
                        foreach (var type in types)
                        {
                            var attr = type.GetCustomAttribute<TableIdentifier>();
                            string? tableName = attr?.Name;
                            if (tableName == null)
                            {
                                Logger.Log($"Type {type.Name} does not have a TableIdentifier attribute. Looking for All Table Names, At The ForeachLoop", logStatus.error);
                                continue;
                            }
                            modelProperties.Add(tableName);
                        }
                        return modelProperties;
                    }
                } catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be fully loaded
                    continue;
                }
            }

            return new List<string>(); // Return empty if no types found
        }

        public static Type[]? GetAllModels(string nameSpace)
        {
            // Get all loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = new List<Type>();
            foreach (var assembly in assemblies)
            {
                types.AddRange(assembly
                    .GetTypes()
                    .Where(t => t.IsClass && t.Namespace == nameSpace)
                    .ToList());
            }
            return types.ToArray();
        }

        public static DataTable ListToDataTable<T>(List<T> items, List<Type> excludedAttributes)
        {
            var table = new DataTable();
            var props = GetPropertiesInfo<T>(excludedAttributes);
            foreach (var prop in props)
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                Logger.Log(prop.Name, logStatus.warning);
                table.Columns.Add(prop.Name, type);
            }

            foreach (var item in items)
            {
                var row = table.NewRow();
                foreach (var prop in props)
                {
                    object value = prop.GetValue(item) ?? DBNull.Value;
                    row[prop.Name] = value;
                    Logger.Log($"name: {prop.Name} value {value}", logStatus.warning);
                }
                table.Rows.Add(row);
            }

            return table;
        }

        public static List<(string tableName, List<string> columns)> TableInfo(Type modelType)
        {
            if (modelType == null)
                throw new ArgumentNullException(nameof(modelType));

            // Get table name from TableIdentifier attribute, fallback to type name
            var attr = modelType.GetCustomAttribute<TableIdentifier>();
            var tableName = attr?.Name ?? modelType.Name;

            // Get properties that are readable, public, and not marked with [NotColumn]
            var columns = modelType.GetProperties()
                .Where(p => p.CanRead &&
                            p.GetMethod?.IsPublic == true &&
                            !Attribute.IsDefined(p, typeof(NotColumn)))
                .Select(p => p.Name)
                .ToList();

            return new List<(string tableName, List<string> columns)> { (tableName, columns) };
        }

    }
}
