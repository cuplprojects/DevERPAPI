using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class PaperType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TypeId { get; set; }
        public string Types { get; set; }
        public bool Status { get; set; }
        public List<int> AssociatedProcessId { get; set; }
        public List<int> RequiredProcessId { get; set; }

    }
}
