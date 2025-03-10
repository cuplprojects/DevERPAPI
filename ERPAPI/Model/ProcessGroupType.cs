using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class ProcessGroupType
    {

        public int Sequence {  get; set; }
        public bool Installed { get; set; } 
        public bool Configurable { get; set; }
        public int TypeId { get; set; }
        public int GroupId { get; set; }
        public int ProcessId { get; set; }
    }
}
