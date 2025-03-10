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
        public string CatchNo { get; set; }

        public string? Paper {  get; set; }
        
        public string ExamDate { get; set; }

        public string ExamTime { get; set; }

        public string Course { get; set; }
        public string Subject { get; set; }
        public string? InnerEnvelope { get; set; }
        public int? OuterEnvelope { get; set; }
        public string? LotNo { get; set; }
        public double Quantity { get; set; }
        public int? Pages { get; set; }
        public double PercentageCatch { get; set; }
        public int ProjectId { get; set; }
        public int? Status { get; set; }
        public int? StopCatch { get; set; }
        public List<int> ProcessId { get; set; } 

    }


}
