using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class DisplaySettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DisplaySettingsId { get; set; }
        
        public int GMRotation { get; set; } //seconds

        public string NotificationColors { get; set; }

        public string FontSettings { get; set; }
    }
}
