using System;

namespace DevCoreHospital.Models
{
    public class AdminNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DoctorId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Status { get; set; } = "UNREAD"; 
    }
}