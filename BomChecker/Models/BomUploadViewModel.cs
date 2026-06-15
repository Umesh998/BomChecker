namespace BomChecker.Models
{
    public class BomUploadViewModel
    {
        public List<string> ColumnHeaders { get; set; } = new();
        public string CacheKey { get; set; } = "";
        public string FileName { get; set; } = "";
        public int TotalRows { get; set; }
    }
}
