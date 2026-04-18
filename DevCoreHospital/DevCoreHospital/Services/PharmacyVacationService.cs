using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class PharmacyVacationService : IPharmacyVacationService
    {
        private const int MaxVacationDaysPerMonth = 4;

        private readonly StaffRepository staffRepository;
        private readonly ShiftRepository shiftRepository;

        public PharmacyVacationService(StaffRepository staffRepository, ShiftRepository shiftRepository)
        {
            this.staffRepository = staffRepository ?? throw new ArgumentNullException(nameof(staffRepository));
            this.shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
        }

        public IReadOnlyList<Pharmacyst> GetPharmacists()
        {
            return staffRepository
                .GetPharmacists()
                .OrderBy(pharmacist => pharmacist.FirstName)
                .ThenBy(pharmacist => pharmacist.LastName)
                .ToList();
        }

        public void RegisterVacation(int pharmacistStaffId, DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var endExclusive = endDate.Date.AddDays(1);

            if (endDate.Date < start)
            {
                throw new ArgumentException("End date must be on or after start date.");
            }

            var pharmacist = staffRepository
                .GetPharmacists()
                .FirstOrDefault(existingPharmacist => existingPharmacist.StaffID == pharmacistStaffId)
                ?? throw new ArgumentException("Pharmacist not found.");

            var pharmacistShifts = shiftRepository.GetShiftsByStaffID(pharmacistStaffId);

            var overlappingShift = pharmacistShifts.FirstOrDefault(shift =>
                start < shift.EndTime && endExclusive > shift.StartTime);

            if (overlappingShift is not null)
            {
                throw new InvalidOperationException(
                    "Cannot add vacation: this period overlaps an existing shift.");
            }

            if (WouldExceedMonthlyVacationLimit(pharmacistShifts, start, endExclusive, MaxVacationDaysPerMonth))
            {
                throw new InvalidOperationException(
                    $"Cannot add vacation: pharmacist would exceed {MaxVacationDaysPerMonth} vacation days in a month.");
            }

            var allShifts = shiftRepository.GetShifts();
            var nextId = allShifts.Count == 0 ? 1 : allShifts.Max(shift => shift.Id) + 1;

            var vacationShift = new Shift(
                nextId,
                pharmacist,
                "Vacation",
                start,
                endExclusive,
                ShiftStatus.VACATION);

            shiftRepository.AddShift(vacationShift);
        }

        private static bool WouldExceedMonthlyVacationLimit(
            IEnumerable<Shift> staffShifts,
            DateTime newStartInclusive,
            DateTime newEndExclusive,
            int maxDaysPerMonth)
        {
            var daysByMonth = new Dictionary<(int Year, int Month), HashSet<DateTime>>();

            foreach (var shift in staffShifts.Where(existingShift => existingShift.Status == ShiftStatus.VACATION))
            {
                AddShiftDaysToBuckets(daysByMonth, shift.StartTime.Date, shift.EndTime.Date);
            }

            AddShiftDaysToBuckets(daysByMonth, newStartInclusive.Date, newEndExclusive.Date);

            return daysByMonth.Values.Any(daysInMonth => daysInMonth.Count > maxDaysPerMonth);
        }

        private static void AddShiftDaysToBuckets(
            Dictionary<(int Year, int Month), HashSet<DateTime>> buckets,
            DateTime startInclusive,
            DateTime endExclusive)
        {
            for (var day = startInclusive.Date; day < endExclusive.Date; day = day.AddDays(1))
            {
                var key = (day.Year, day.Month);
                if (!buckets.TryGetValue(key, out var daysInMonth))
                {
                    daysInMonth = new HashSet<DateTime>();
                    buckets[key] = daysInMonth;
                }

                daysInMonth.Add(day);
            }
        }
    }
}
