using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPGenericFunctions.Model
{
    public class UserAuth
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int UserId { get; set; }

        public bool AutogenPass { get; set; }

        public string Password { get; set; }

        // Security Question IDs
        public int? SecurityQuestion1Id { get; set; }
        public int? SecurityQuestion2Id { get; set; }

        // Security Answers
        public string? SecurityAnswer1 { get; set; }
        public string? SecurityAnswer2 { get; set; }
        public int ScreenLockPin { get; set; } = 123;
    }
}
