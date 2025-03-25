using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class UserDisplay
    {
        [Key]
        public int UserId { get; set; }
        public int DisplayId { get; set; }
    }
}
