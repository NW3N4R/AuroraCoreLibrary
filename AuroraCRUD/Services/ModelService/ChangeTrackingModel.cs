namespace AuroraCRUD.Services.ModelService
{
    public class ChangeTrackingModel
    {
        public long ChangeVersion { get; set; }
        public string Operation { get; set; } = string.Empty;
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;

    }
}
