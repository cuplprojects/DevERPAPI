using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class ExamType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExamTypeId { get; set; }
        public string TypeName { get; set; }
        public string Type { get; set; }
    }
}
