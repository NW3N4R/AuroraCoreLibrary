namespace AuroraCRUD.Attributes
{
    [System.AttributeUsage(AttributeTargets.Property)]
    public class PrimaryKey : System.Attribute
    {
        private readonly string name;
        public PrimaryKey(string Name = "id")
        {
            name = Name;
        }
    }
}
