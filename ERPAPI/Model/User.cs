using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPGenericFunctions.Model
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Required]
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public int RoleId { get; set; }
        public string MobileNo { get; set; }
        public bool Status { get; set; }
        public string Gender { get; set; }
        public string Address { get; set; }
        public string? ProfilePicturePath { get; set; }
        public int LocationId { get; set; }
    }
}
