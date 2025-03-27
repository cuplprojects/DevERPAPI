using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace ERPAPI.Model
{
    public class QuantitySheet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuantitySheetId { get; set; }

        [Required]
        [StringLength(15)]
        public string? CatchNo { get; set; }
        public string? ExamDate { get; set; }
        public string? ExamTime { get; set; }
        public int CourseId { get; set; }
        public int SubjectId { get; set; }
        public string? InnerEnvelope { get; set; }
        public int? OuterEnvelope { get; set; }
        [StringLength(4)]
        public string? LotNo { get; set; }
        public double Quantity { get; set; }
        public int? Pages { get; set; }
        public double PercentageCatch { get; set; }
        public int ProjectId { get; set; }
        public int? Status { get; set; }
        public int? StopCatch { get; set; }
        public List<int>? ProcessId { get; set; } = new List<int>();
        [StringLength(45)]
        public string? PaperNumber { get; set; }
        public string? PaperTitle { get; set; }
        public int QPId { get; set; }
        public int MaxMarks { get; set; }

        [StringLength(30)]
        public string? Duration { get; set; }
        public List<int>? LanguageId { get; set; }
        public int ExamTypeId { get; set; }
        [StringLength(45)]
        public string? NEPCode { get; set; }
        [StringLength(45)]
        public string? PrivateCode { get; set; }
        public int MSSStatus { get; set; }

        public int? TTFStatus { get; set; }
      

    }


}
