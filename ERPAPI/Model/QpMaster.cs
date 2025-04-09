using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class QpMaster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QPMasterId { get; set; }
        public int GroupId { get; set; }
        public int? TypeId { get; set; }
        public string? NEPCode { get; set; }
        public string? UniqueCode { get; set; }
        public int? SubjectId { get; set; }
        public string? PaperNumber { get; set; }
        public string? PaperTitle { get; set; }
        public int? MaxMarks { get; set; }
        public string? Duration { get; set; }
        public List<int>? LanguageId { get; set; }
        public string? StructureOfPaper { get; set; }
        public string? CustomizedField2 { get; set; }
        public string? CustomizedField3 { get; set; }
        public int? CourseId { get; set; }
        public int? ExamTypeId { get; set; }
    }
}
