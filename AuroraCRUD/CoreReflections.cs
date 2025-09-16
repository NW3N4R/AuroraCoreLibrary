using AuroraCRUD.Attributes;

using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System.Runtime.Serialization;

namespace AuroraCRUD
{
    public static class CoreReflections
    {
        public static (Type? observable, Type? model) GetObservableCollectionType<T>(string observName) where T : class
        {
            var property = typeof(T)
                .GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(TableIdentifier)))
                .ToList();

            var observ = property.FirstOrDefault(x => x.GetCustomAttribute<TableIdentifier>()?.Name == observName);
            if (observ != null)
            {
                var ObservType = observ.PropertyType;
                var innerType = ObservType.GetGenericArguments()[0];
                return (ObservType, innerType);
            }

            return (null, null);
        }

        public static object? GetObservableCollectionType(object tempStore, string name)
        {
            var property = tempStore.GetType()
       .GetProperties()
       .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                   p.PropertyType.GetGenericTypeDefinition() == typeof(ObservableCollection<>) &&
                   p?.GetCustomAttribute<TableIdentifier>()?.Name == name);

            var collectionObj = property?.GetValue(tempStore);
            if (property == null)
                return null;

            if (collectionObj is System.Collections.IEnumerable enumerable &&
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                dynamic baseCollection = collectionObj;
                return baseCollection;
            }
            return null;
        }

        public static PropertyInfo[] GetPropertiesInfo<T>(List<Type> excludedAttributes)
        {
            var excludedTypes = excludedAttributes.Select(a => a.GetType()).ToList();

            var properties = typeof(T).GetProperties()
                .Where(x => !excludedTypes
                .Any(a => Attribute.IsDefined(x, a)))
                .ToArray();

            return properties;
        }

        public static string[] GetPropertiesName<T>(List<Type> excludedAttributes)
        {
            var excludedTypes = excludedAttributes.Select(a => a.GetType()).ToList();

            var properties = typeof(T).GetProperties()
                .Where(x => !excludedTypes
                .Any(a => Attribute.IsDefined(x, a)))
                .Select(x => x.Name)
                .ToArray();

            return properties;
        }

        public static string[] GetSQLParameters<T>(List<Type> excludedAttributes)
        {
            var excludedTypes = excludedAttributes.Select(a => a.GetType()).ToList();

            var properties = typeof(T).GetProperties()
                .Where(x => !excludedTypes
                .Any(a => Attribute.IsDefined(x, a)))
                .Select(x => "@" + x.Name)
                .ToArray();

            return properties;
        }

        public static PropertyInfo[] GetPrimaryKey<T>()
        {
            return typeof(T).GetProperties().Where(x => Attribute.IsDefined(x, typeof(PrimaryKey))).ToArray();
        }

        public static PropertyInfo[] GetForeinKey<T>()
        {
            return typeof(T).GetProperties().Where(x => Attribute.IsDefined(x, typeof(ForeignKey))).ToArray();
        }

        public static string GetTableNameFromModel<T>()
        {
            var tableAttr = typeof(T).GetCustomAttribute<TableIdentifier>();
            if (tableAttr == null)
                throw new InvalidOperationException($"Type {typeof(T).Name} does not have a TableIdentifier attribute.");

            return tableAttr.Name;
        }

        public static List<string> GetAllTableNames(string nameSpace)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var modelProperties = new List<string>();

            var types = assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace == nameSpace);
            // "AuroraMarketDesktop.Models" is name space of the models

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<TableIdentifier>();
                string? tableName = attr?.Name;
                if (tableName == null)
                {
                    throw new InvalidDataContractException("no table");
                }
                modelProperties.Add(tableName);
            }

            return modelProperties;
        }

        public static DataTable ListToDataTable<T>(List<T> items, List<Type> excludedAttributes)
        {
            var table = new DataTable();
            var props = GetPropertiesInfo<T>(new List<Type> { typeof(PrimaryKey) });
            foreach (var prop in props)
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                table.Columns.Add(prop.Name, type);
            }

            foreach (var item in items)
            {
                var row = table.NewRow();
                foreach (var prop in props)
                {
                    object value = prop.GetValue(item) ?? DBNull.Value;
                    row[prop.Name] = value;
                }
                table.Rows.Add(row);
            }

            return table;
        }

    }
}
