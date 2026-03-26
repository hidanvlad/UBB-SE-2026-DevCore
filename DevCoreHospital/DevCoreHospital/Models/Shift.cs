using System;

namespace DevCoreHospital.Models
{
    public enum ShiftStatus
    {
        SCHEDULED,
        ACTIVE,
        COMPLETED,
        CANCELLED
    }

    public class Shift
    {
        // --- UML fields ---
        public int ShiftID { get; set; }
        public Staff? AppointedStaff { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public ShiftStatus Status { get; set; } = ShiftStatus.SCHEDULED;

        public bool IsActive() => Status == ShiftStatus.ACTIVE;

        // --- Compatibility aliases used across the existing app ---
        public int Id
        {
            get => ShiftID;
            set => ShiftID = value;
        }

        // Existing in-memory pharmacy shift generation uses StaffId + RotationAssignment.
        public int StaffId
        {
            get => AppointedStaff?.StaffID ?? 0;
            set
            {
                AppointedStaff ??= new Staff();
                AppointedStaff.StaffID = value;
            }
        }

        // Used only by mock fatigue table
        public string DoctorId { get; set; } = "";

        public string RotationAssignment { get; set; } = "";

        public DateTime StartTime
        {
            get => Start;
            set => Start = value;
        }

        public DateTime? EndTime
        {
            get => End == default ? null : End;
            set => End = value ?? default;
        }
    }
}