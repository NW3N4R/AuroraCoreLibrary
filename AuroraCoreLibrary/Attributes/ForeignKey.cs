namespace AuroraCoreLibrary.Attributes
{
    [System.AttributeUsage(AttributeTargets.Property)]
    public class ForeignKey : System.Attribute
    {
        private readonly string name;
        public ForeignKey(string Name = "pid")
        {
            name = Name;
        }
    }
}
