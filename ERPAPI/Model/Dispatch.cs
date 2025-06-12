using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class Dispatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // This ensures the Id is auto-generated
        public int Id { get; set; }

        public int ProcessId { get; set; }

        public int ProjectId { get; set; }

        [StringLength(255)]  // Limits the length to 255 characters for LotNo
        public string LotNo { get; set; }

        public int BoxCount { get; set; }

        [StringLength(255)]  // Limits the length to 255 characters for MessengerName
        public string MessengerName { get; set; }

        [StringLength(15)]  // Limits the length to 15 characters for MessengerMobile
        public string MessengerMobile { get; set; }

        [StringLength(50)]  // Limits the length to 50 characters for DispatchMode
        public string DispatchMode { get; set; }

        [StringLength(50)]  // Limits the length to 50 characters for VehicleNumber
        public string? VehicleNumber { get; set; }

        [StringLength(255)]  // Limits the length to 255 characters for DriverName
        public string? DriverName { get; set; }

        [StringLength(15)]  // Limits the length to 15 characters for DriverMobile
        public string? DriverMobile { get; set; }

        public DateTime? CreatedAt { get; set; } // Timestamp for when the dispatch is created

        public DateTime? UpdatedAt { get; set; } // Timestamp for when the dispatch is last updated

        public bool Status { get; set; } // Status of the dispatch (true or false)
        public DateTime? DispatchDate { get; set; } // Date when the dispatch is made
        public int? ModeCount { get; set; } // Count of modes used in the dispatch
    }
}
