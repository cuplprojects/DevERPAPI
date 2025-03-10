using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionId { get; set; }
        public int InterimQuantity { get; set; }
        public string Remarks { get; set; }
        public string VoiceRecording { get; set; }
        public int ProjectId { get; set; }
        public int QuantitysheetId { get; set; }
        public int ProcessId { get; set; }
        public int ZoneId { get; set; }
        public int MachineId { get; set; }
        public int Status { get; set; }
        public string AlarmId { get; set; }
        public int LotNo { get; set; }
        public List<int> TeamId { get; set; }
       
    }
}
