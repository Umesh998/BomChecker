namespace BomChecker.Models
{
    public class BomVerifyRequest
    {
        public string CacheKey { get; set; } = "";
        public string DescriptionColumn { get; set; } = "";
        public List<string> PartNumberColumns { get; set; } = new();
    }
}
