using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPAPI.Model
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int PriorityOrder { get; set; }
        public bool Status { get; set; }
        public string Permission { get; set; }

        [NotMapped]
        public List<string> PermissionList
        {
            get => string.IsNullOrEmpty(Permission) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(Permission);
            set => Permission = JsonConvert.SerializeObject(value);
        }
    }
}
