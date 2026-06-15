namespace BomChecker.Models
{
    public class BomVerifyResult
    {
        public string FileName { get; set; } = "";
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public List<string> PartNumberColumns { get; set; } = new();
        public List<BomRow> Rows { get; set; } = new();

        // Summary counts
        public int StrongMatchCount => Rows.Count(r =>
            r.PartResults.Any(p => p.MatchScore >= 80));
        public int PartialMatchCount => Rows.Count(r =>
            r.PartResults.Any(p => p.MatchScore >= 50 && p.MatchScore < 80) &&
            r.PartResults.All(p => p.MatchScore < 80));
        public int WeakMatchCount => Rows.Count(r =>
            r.PartResults.All(p => p.MatchScore < 50 && p.Found));
        public int NotFoundCount => Rows.Count(r =>
            r.PartResults.All(p => !p.Found));
    }
}
