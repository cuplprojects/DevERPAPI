using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ERPAPI.Model
{
    public class Team
    {
        [Key]
        public int TeamId { get; set; } // Primary Key

        [Required]
        [StringLength(255)]
        public string TeamName { get; set; } // Name of the team

        public int ProcessId { get; set; } // Associated Process ID

        // List of User IDs associated with the team
        public List<int> UserIds { get; set; } = new List<int>(); // IDs of members associated with the team

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Creation date, default to now

        public int CreatedBy { get; set; } // User ID of the creator

        public bool Status { get; set; } = true; // Active status of the team
    }
}
