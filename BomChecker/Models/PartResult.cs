namespace BomChecker.Models
{
    public class PartResult
    {
        public string ColumnName { get; set; } = "";
        public string PartNumber { get; set; } = "";
        public string ApiDescription { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Package { get; set; } = "N/A";
        public string MslLevel { get; set; } = "N/A";
        public string MountType { get; set; } = "N/A";
        public double MatchScore { get; set; } = 0;
        public string MatchVerdict { get; set; } = "";
        public string Source { get; set; } = "";
        public string ProductUrl { get; set; } = "";
        public bool Found { get; set; } = false;
    }
}
