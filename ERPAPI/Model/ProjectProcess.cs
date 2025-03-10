using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class ProjectProcess
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int ProcessId { get; set; }
        public double Weightage { get; set; }
        public int Sequence {  get; set; }
        public List<int> FeaturesList { get; set; }
        public List<int> UserId { get; set; } = new List<int>();
        public int? ThresholdQty { get; set; } = null;

    }
}
