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
        private const int OneDay = 1;
        private const int FirstShiftId = 1;
        private const int IdIncrement = 1;
        private const int EmptyShiftCollectionCount = 0;
        private const string VacationShiftLocationLabel = "Vacation";

        private readonly IPharmacyStaffRepository staffRepository;
        private readonly IPharmacyShiftRepository shiftRepository;

        public PharmacyVacationService(IPharmacyStaffRepository staffRepository, IPharmacyShiftRepository shiftRepository)
        {
            this.staffRepository = staffRepository ?? throw new ArgumentNullException(nameof(staffRepository));
            this.shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
        }

        public IReadOnlyList<Pharmacyst> GetPharmacists()
        {
            string ByFirstName(Pharmacyst pharmacist) => pharmacist.FirstName;
            string ByLastName(Pharmacyst pharmacist) => pharmacist.LastName;

            return staffRepository
                .GetPharmacists()
                .OrderBy(ByFirstName)
                .ThenBy(ByLastName)
                .ToList();
        }

        public void RegisterVacation(int pharmacistStaffId, DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var endExclusive = endDate.Date.AddDays(OneDay);

            if (endDate.Date < start)
            {
                throw new ArgumentException("End date must be on or after start date.");
            }

            bool HasMatchingStaffId(Pharmacyst existingPharmacist) => existingPharmacist.StaffID == pharmacistStaffId;
            var pharmacist = staffRepository
                .GetPharmacists()
                .FirstOrDefault(HasMatchingStaffId)
                ?? throw new ArgumentException("Pharmacist not found.");

            bool IsForPharmacist(Shift existingShift) => existingShift.AppointedStaff.StaffID == pharmacistStaffId;
            var pharmacistShifts = shiftRepository.GetAllShifts().Where(IsForPharmacist).ToList();

            bool OverlapsVacationPeriod(Shift shift) => start < shift.EndTime && endExclusive > shift.StartTime;
            var overlappingShift = pharmacistShifts.FirstOrDefault(OverlapsVacationPeriod);

            if (overlappingShift is not null)
            {
                throw new InvalidOperationException("Cannot add vacation: this period overlaps an existing shift.");
            }

            if (WouldExceedMonthlyVacationLimit(pharmacistShifts, start, endExclusive, MaxVacationDaysPerMonth))
            {
                throw new InvalidOperationException(
                    $"Cannot add vacation: pharmacist would exceed {MaxVacationDaysPerMonth} vacation days in a month.");
            }

            int ByShiftId(Shift shift) => shift.Id;
            var allShifts = shiftRepository.GetAllShifts();
            var nextId = allShifts.Count == EmptyShiftCollectionCount
                ? FirstShiftId
                : allShifts.Max(ByShiftId) + IdIncrement;

            var vacationShift = new Shift(
                nextId,
                pharmacist,
                VacationShiftLocationLabel,
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

            bool IsVacationShift(Shift existingShift) => existingShift.Status == ShiftStatus.VACATION;

            foreach (var shift in staffShifts.Where(IsVacationShift))
            {
                AddShiftDaysToBuckets(daysByMonth, shift.StartTime.Date, shift.EndTime.Date);
            }

            AddShiftDaysToBuckets(daysByMonth, newStartInclusive.Date, newEndExclusive.Date);

            bool ExceedsLimit(HashSet<DateTime> daysInMonth) => daysInMonth.Count > maxDaysPerMonth;

            return daysByMonth.Values.Any(ExceedsLimit);
        }

        private static void AddShiftDaysToBuckets(
            Dictionary<(int Year, int Month), HashSet<DateTime>> buckets,
            DateTime startInclusive,
            DateTime endExclusive)
        {
            for (var day = startInclusive.Date; day < endExclusive.Date; day = day.AddDays(OneDay))
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
