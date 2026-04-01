using System;
using System.Globalization;

namespace DevCoreHospital.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public IStaff AppointedStaff { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; } 
        public ShiftStatus Status { get; set; } = ShiftStatus.SCHEDULED;

        public Shift() { }
        public Shift(int id, IStaff appointedStaff, string location, DateTime startTime, DateTime endTime, ShiftStatus status)
        {
            this.Id = id;
            this.AppointedStaff = appointedStaff;
            this.Location = location;
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.Status = status;
        }

        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

        public string DisplayDayMonth => StartTime.ToString("dd MMM", EnglishCulture);
        public string DisplayDayName => StartTime.ToString("dddd", EnglishCulture);
        public string DisplayStartTime => StartTime.ToString("HH:mm");
        public string DisplayEndTime => EndTime.ToString("HH:mm");
    }
}
