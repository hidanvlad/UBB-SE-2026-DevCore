using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Data
{
    public sealed class MockFatigueShiftDataSource : IFatigueShiftDataSource
    {
        private readonly List<StaffProfile> _staff;
        private readonly List<RosterShift> _shifts;

        public MockFatigueShiftDataSource()
        {
            var monday = StartOfWeek(DateTime.Today);

            _staff = new List<StaffProfile>
            {
                new() { StaffId = 1, FullName = "Dr. Alice Morgan", Role = "Doctor", Specialization = "Cardiology", IsAvailable = false },
                new() { StaffId = 2, FullName = "Dr. Mihai Pop", Role = "Doctor", Specialization = "Cardiology", IsAvailable = false },
                new() { StaffId = 3, FullName = "Dr. Ana Ionescu", Role = "Doctor", Specialization = "Cardiology", IsAvailable = false },
                new() { StaffId = 4, FullName = "Dr. Raul Petrescu", Role = "Doctor", Specialization = "ER", IsAvailable = false },
                new() { StaffId = 5, FullName = "Pharm. Teodora Rusu", Role = "Pharmacist", Specialization = "General", IsAvailable = false }
            };

            _shifts = new List<RosterShift>
            {
                // Staff 1 intentionally violates both rules (66h total + 10h rest gap)
                NewShift(101, 1, "Doctor", "Cardiology", monday.AddHours(8), monday.AddHours(20)),
                NewShift(102, 1, "Doctor", "Cardiology", monday.AddDays(1).AddHours(6), monday.AddDays(1).AddHours(18)),
                NewShift(103, 1, "Doctor", "Cardiology", monday.AddDays(2).AddHours(8), monday.AddDays(2).AddHours(20)),
                NewShift(104, 1, "Doctor", "Cardiology", monday.AddDays(3).AddHours(8), monday.AddDays(3).AddHours(20)),
                NewShift(105, 1, "Doctor", "Cardiology", monday.AddDays(4).AddHours(8), monday.AddDays(4).AddHours(20)),
                NewShift(106, 1, "Doctor", "Cardiology", monday.AddDays(5).AddHours(8), monday.AddDays(5).AddHours(14)),

                // Alternate cardiology doctors (eligible suggestions if no overlap)
                NewShift(201, 2, "Doctor", "Cardiology", monday.AddDays(1).AddHours(18), monday.AddDays(1).AddHours(22)),
                NewShift(202, 2, "Doctor", "Cardiology", monday.AddDays(3).AddHours(20), monday.AddDays(3).AddHours(23)),
                NewShift(301, 3, "Doctor", "Cardiology", monday.AddDays(0).AddHours(20), monday.AddDays(0).AddHours(23)),
                NewShift(302, 3, "Doctor", "Cardiology", monday.AddDays(5).AddHours(15), monday.AddDays(5).AddHours(20)),

                // Different specialization/role (should not be selected)
                NewShift(401, 4, "Doctor", "ER", monday.AddDays(2).AddHours(8), monday.AddDays(2).AddHours(16)),
                NewShift(501, 5, "Pharmacist", "General", monday.AddDays(2).AddHours(8), monday.AddDays(2).AddHours(16))
            };
        }

        public IReadOnlyList<RosterShift> GetShiftsForWeek(DateTime weekStart)
        {
            var start = StartOfWeek(weekStart);
            var end = start.AddDays(7);

            return _shifts
                .Where(s => s.Start < end && s.End > start)
                .OrderBy(s => s.Start)
                .ToList();
        }

        public IReadOnlyList<RosterShift> GetAllShifts() => _shifts;

        public IReadOnlyList<StaffProfile> GetStaffProfiles() => _staff;

        public double GetMonthlyWorkedHours(int staffId, int year, int month)
        {
            return _shifts
                .Where(s => s.StaffId == staffId && s.Start.Year == year && s.Start.Month == month)
                .Sum(s => (s.End - s.Start).TotalHours);
        }

        /// <summary>
        /// Reassign a shift from one staff member to another (in-memory operation)
        /// </summary>
        public bool ReassignShift(int shiftId, int newStaffId)
        {
            var shift = _shifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift == null)
                return false;

            var newStaff = _staff.FirstOrDefault(s => s.StaffId == newStaffId);
            if (newStaff == null)
                return false;

            // Update the shift's staff assignment
            shift.StaffId = newStaffId;
            shift.StaffName = newStaff.FullName;

            return true;
        }

        private RosterShift NewShift(int id, int staffId, string role, string specialization, DateTime start, DateTime end)
        {
            var staffName = _staff.First(x => x.StaffId == staffId).FullName;
            return new RosterShift
            {
                Id = id,
                StaffId = staffId,
                StaffName = staffName,
                Role = role,
                Specialization = specialization,
                Start = start,
                End = end
            };
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }
    }
}

