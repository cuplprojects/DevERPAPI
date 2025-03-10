using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
      public class Process
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }  // Auto-incrementing primary key

        public string Name { get; set; }
        public double Weightage { get; set; }
        public bool Status { get; set; }
        public List<int> InstalledFeatures { get; set; }
        public string ProcessType { get; set; }  // New field for process type

        // New properties for range
        public int? RangeStart { get; set; }  // Nullable to allow for independent processes
        public int? RangeEnd { get; set; }    // Nullable to allow for independent processes

    }
}
