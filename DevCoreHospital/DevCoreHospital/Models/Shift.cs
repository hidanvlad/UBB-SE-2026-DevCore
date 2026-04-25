using System;

namespace DevCoreHospital.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public IStaff AppointedStaff { get; set; } = default!;
        public string Location { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ShiftStatus Status { get; set; } = ShiftStatus.SCHEDULED;

        public Shift(int id, IStaff appointedStaff, string location, DateTime startTime, DateTime endTime, ShiftStatus status)
        {
            this.Id = id;
            this.AppointedStaff = appointedStaff;
            this.Location = location;
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.Status = status;
        }
    }
}
