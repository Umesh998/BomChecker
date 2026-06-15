namespace BomChecker.Models
{
    public class PartDetails
    {
        public string Mpn { get; set; } = "";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Category { get; set; } = "";
        public string Package { get; set; } = "";
        public string DatasheetUrl { get; set; } = "";
        public string ProductUrl { get; set; } = "";
        public string Stock { get; set; } = "";
        public string Price { get; set; } = "";
        public string Source { get; set; } = "";
        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
