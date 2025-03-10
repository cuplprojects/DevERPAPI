using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class FeatureEnabling
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        

        public int FeatureId { get; set; }
        public int ProcessId { get; set; }
        public bool IsEnabled { get; set; }
        
    }
}
