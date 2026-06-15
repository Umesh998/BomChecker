namespace BomChecker.Models
{
    public class BomRow
    {
        public int RowNumber { get; set; }
        public string OriginalDescription { get; set; } = "";
        public List<PartResult> PartResults { get; set; } = new();
        public string BestMatchPartNumber { get; set; } = "";
        public double BestMatchScore { get; set; } = 0;
    }
}
