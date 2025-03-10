using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class TextLabel
    {
        [Key]
        public int TextLabelId { get; set; }

        [Required]
        
        public string LabelKey { get; set; }

        [Required]
        public string EnglishLabel { get; set; }

        [Required]
        public string HindiLabel { get; set; }
    }
}
