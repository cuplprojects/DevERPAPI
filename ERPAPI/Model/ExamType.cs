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
<<<<<<< HEAD
<<<<<<< HEAD
        //public string Type { get; set; }
=======
     
>>>>>>> Ranjeet
=======
        //public string Type { get; set; }
>>>>>>> Sarvagya
    }
}
