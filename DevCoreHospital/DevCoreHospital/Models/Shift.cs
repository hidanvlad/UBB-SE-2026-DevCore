using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevCoreHospital.Models
{
    public class Shift
    {
        public string Id { get; set; } = string.Empty;
        public string DoctorId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; } 
        public string Status { get; set; } = "ACTIVE"; // 'ACTIVE' or 'COMPLETED'
    }
}
