using System.ComponentModel.DataAnnotations;

namespace BomChecker.Models
{
    public class BomReport
    {
        public int Id { get; set; }

        [MaxLength(260)]
        public string FileName { get; set; } = "";

        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }

        public List<BomRowEntity> Rows { get; set; } = new();
    }
}
