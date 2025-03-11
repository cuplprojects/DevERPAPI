using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectId { get; set; }
        public int GroupId { get; set; }
        public int TypeId { get; set; }
        public string? Name { get; set; }
        public bool Status { get; set; }
        public string? Description { get; set; }
        public int? NoOfSeries { get; set; }
        public string? SeriesName { get; set; }
        public DateTime Date { get; set; }
        public string? QuantityThreshold { get; set; }
        public int SessionId { get; set; }
        public int ExamTypeId { get; set; }
    }


}
