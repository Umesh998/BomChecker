using System.ComponentModel.DataAnnotations;

namespace BomChecker.Models
{
    public class BomRowEntity
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public BomReport Report { get; set; } = null!;

        public int RowNumber { get; set; }

        [MaxLength(1000)]
        public string OriginalDescription { get; set; } = "";

        [MaxLength(200)]
        public string BestMatchPartNumber { get; set; } = "";

        public double BestMatchScore { get; set; }

        public List<PartResultEntity> PartResults { get; set; } = new();
    }
}
