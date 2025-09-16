namespace AuroraCRUD.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class TableIdentifier : System.Attribute
    {
        public string Name { get; }
        public TableIdentifier(string value)
        {
            Name = value;
        }
    }
}
