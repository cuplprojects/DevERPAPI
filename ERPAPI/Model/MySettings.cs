using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class MySettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
<<<<<<< HEAD

=======
>>>>>>> roy
        public int SettingId { get; set; }
        public string Settings { get; set; }
        public int UserId { get; set; }
    }
}
