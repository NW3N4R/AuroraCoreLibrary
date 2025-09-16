namespace AuroraCRUD.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKey : Attribute
    {
        private readonly string name;
        public ForeignKey(string Name = "pid")
        {
            name = Name;
        }
    }
}
