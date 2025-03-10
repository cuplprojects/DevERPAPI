using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    [Table("catchteams")]
    public class CatchTeam
    {
        [Key]
        public int QuantitySheetId { get; set; } // Primary Key

        public string Members { get; set; } // Members associated with the catch team (stored as a comma-separated string)
    }
}
