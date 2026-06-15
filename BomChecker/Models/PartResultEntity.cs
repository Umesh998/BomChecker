using System.ComponentModel.DataAnnotations;

namespace BomChecker.Models
{
    public class PartResultEntity
    {
        public int Id { get; set; }
        public int BomRowId { get; set; }
        public BomRowEntity BomRow { get; set; } = null!;

        [MaxLength(200)]
        public string ColumnName { get; set; } = "";

        [MaxLength(200)]
        public string PartNumber { get; set; } = "";

        [MaxLength(1000)]
        public string ApiDescription { get; set; } = "";

        [MaxLength(200)]
        public string Manufacturer { get; set; } = "";

        [MaxLength(200)]
        public string Package { get; set; } = "";

        [MaxLength(100)]
        public string MslLevel { get; set; } = "";

        [MaxLength(50)]
        public string MountType { get; set; } = "";

        public double MatchScore { get; set; }

        [MaxLength(100)]
        public string MatchVerdict { get; set; } = "";

        [MaxLength(50)]
        public string Source { get; set; } = "";

        [MaxLength(500)]
        public string ProductUrl { get; set; } = "";

        public bool Found { get; set; }
    }
}
