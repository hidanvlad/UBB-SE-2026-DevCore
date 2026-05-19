using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class ShiftManagementService : IShiftManagementService
    {
        private const int DaysInWeek = 7;
        private const int NewShiftPlaceholderId = 0;
        private const string PharmacyLocationLabel = "Pharmacy";

        private readonly IShiftManagementStaffRepository staffRepository;
        private readonly IShiftManagementShiftRepository shiftRepository;

        public ShiftManagementService(IShiftManagementStaffRepository staffRepository, IShiftManagementShiftRepository shiftRepository)
        {
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public void SetShiftActive(int shiftId)
        {
            bool HasMatchingId(Shift existingShift) => existingShift.Id == shiftId;
            var shift = shiftRepository.GetAllShifts().FirstOrDefault(HasMatchingId);
            if (shift != null)
            {
                shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);
                staffRepository.UpdateStaffAvailability(shift.AppointedStaff.StaffID, true, DoctorStatus.AVAILABLE);
            }
        }

        public void CancelShift(int shiftId)
        {
            bool HasMatchingId(Shift existingShift) => existingShift.Id == shiftId;
            var shift = shiftRepository.GetAllShifts().FirstOrDefault(HasMatchingId);
            if (shift != null)
            {
                staffRepository.UpdateStaffAvailability(shift.AppointedStaff.StaffID, false, DoctorStatus.OFF_DUTY);
                shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.COMPLETED);
            }
        }

        public bool ValidateNoOverlap(int staffId, DateTime start, DateTime end)
        {
            bool OverlapsWithStaff(Shift shift) =>
                (shift.AppointedStaff.StaffID == staffId) &&
                ((start >= shift.StartTime && start < shift.EndTime) || (end > shift.StartTime && end <= shift.EndTime));

            return !shiftRepository.GetAllShifts().Any(OverlapsWithStaff);
        }

        public void AddShift(Shift shift) => shiftRepository.AddShift(shift);

        public bool TryAddShift(IStaff staff, DateTime start, DateTime end, string location)
        {
            if (!ValidateNoOverlap(staff.StaffID, start, end))
            {
                return false;
            }

            var newShift = new Shift(NewShiftPlaceholderId, staff, location, start, end, ShiftStatus.SCHEDULED);
            shiftRepository.AddShift(newShift);
            return true;
        }

        public bool ValidateShiftTimes(TimeSpan start, TimeSpan end) => end > start;

        public List<Shift> GetDailyShifts(DateTime date)
        {
            bool IsOnDate(Shift shift) => shift.StartTime.Date == date.Date;
            return GetHydratedShifts().Where(IsOnDate).ToList();
        }

        public List<Shift> GetWeeklyShifts(DateTime date)
        {
            var weekStart = date.AddDays(-(int)DateTime.Now.DayOfWeek + (int)DayOfWeek.Monday);
            var weekEnd = weekStart.AddDays(DaysInWeek);

            bool IsInWeek(Shift shift) => shift.StartTime >= weekStart && shift.StartTime < weekEnd;
            return GetHydratedShifts().Where(IsInWeek).ToList();
        }

        public bool ReassignShift(Shift shift, IStaff newStaff)
        {
            if (shift == null || newStaff == null)
            {
                return false;
            }

            shift.AppointedStaff = newStaff;
            return true;
        }

        public List<IStaff> GetFilteredStaff(string location, string requiredSpecializationOrCertification)
        {
            var allStaff = staffRepository.LoadAllStaff();
            var filteredStaff = new List<IStaff>();

            bool PharmacistHasCertification(Pharmacyst pharmacist) =>
                pharmacist.Certification.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase);
            bool DoctorHasSpecialization(Doctor doctor) =>
                doctor.Specialization.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase);

            if (location.Equals(PharmacyLocationLabel, StringComparison.OrdinalIgnoreCase))
            {
                filteredStaff.AddRange(allStaff.OfType<Pharmacyst>().Where(PharmacistHasCertification));
            }
            else
            {
                filteredStaff.AddRange(allStaff.OfType<Doctor>().Where(DoctorHasSpecialization));
            }

            return filteredStaff;
        }

        public List<IStaff> FindStaffReplacements(Shift shift)
        {
            if (shift == null || shift.AppointedStaff == null)
            {
                return new List<IStaff>();
            }

            var currentStaff = shift.AppointedStaff;
            var allStaff = staffRepository.LoadAllStaff();

            bool IsEligibleReplacement(IStaff staffMember) =>
                staffMember.GetType() == currentStaff.GetType() &&
                staffMember.StaffID != currentStaff.StaffID &&
                ValidateNoOverlap(staffMember.StaffID, shift.StartTime, shift.EndTime);

            return allStaff.Where(IsEligibleReplacement).ToList();
        }

        public List<string> GetSpecializationsAndCertificationsForLocation(string location)
        {
            var qualificationNames = new List<string>();
            var allStaff = staffRepository.LoadAllStaff();

            bool HasCertification(Pharmacyst pharmacist) => !string.IsNullOrEmpty(pharmacist.Certification);
            string ToCertification(Pharmacyst pharmacist) => pharmacist.Certification;
            bool HasSpecialization(Doctor doctor) => !string.IsNullOrEmpty(doctor.Specialization);
            string ToSpecialization(Doctor doctor) => doctor.Specialization;
            string IdentityName(string name) => name;

            if (location.Equals(PharmacyLocationLabel, StringComparison.OrdinalIgnoreCase))
            {
                qualificationNames.AddRange(allStaff.OfType<Pharmacyst>()
                    .Where(HasCertification)
                    .Select(ToCertification));
            }
            else
            {
                qualificationNames.AddRange(allStaff.OfType<Doctor>()
                    .Where(HasSpecialization)
                    .Select(ToSpecialization));
            }

            return qualificationNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(IdentityName).ToList();
        }

        public float GetWeeklyHours(int staffId)
        {
            var now = DateTime.Now;
            int daysFromMonday = (DaysInWeek + (now.DayOfWeek - DayOfWeek.Monday)) % DaysInWeek;
            var weekStart = now.Date.AddDays(-daysFromMonday);
            var weekEnd = weekStart.AddDays(DaysInWeek);

            bool IsForStaffInWeek(Shift shift) => shift.AppointedStaff.StaffID == staffId && shift.StartTime >= weekStart && shift.StartTime < weekEnd;
            float ToShiftHours(Shift shift) => (float)(shift.EndTime - shift.StartTime).TotalHours;

            return shiftRepository.GetAllShifts()
                .Where(IsForStaffInWeek)
                .Sum(ToShiftHours);
        }

        public List<Shift> GetActiveShifts()
        {
            bool IsActiveShift(Shift shift) => shift.Status == ShiftStatus.ACTIVE;
            return GetHydratedShifts().Where(IsActiveShift).ToList();
        }

        private List<Shift> GetHydratedShifts()
        {
            int ByStaffId(IStaff staffMember) => staffMember.StaffID;
            var staffById = (staffRepository.LoadAllStaff() ?? new List<IStaff>())
                .ToDictionary(ByStaffId);
            var hydratedShifts = new List<Shift>();
            foreach (var shift in shiftRepository.GetAllShifts() ?? new List<Shift>())
            {
                IStaff appointedStaff = staffById.TryGetValue(shift.AppointedStaff.StaffID, out var resolvedStaff)
                    ? resolvedStaff
                    : shift.AppointedStaff;
                hydratedShifts.Add(new Shift(shift.Id, appointedStaff, shift.Location, shift.StartTime, shift.EndTime, shift.Status));
            }
            return hydratedShifts;
        }

        public bool IsStaffWorkingDuring(int staffId, DateTime startTime, DateTime endTime)
        {
            bool IsWorkingDuring(Shift shift) =>
                shift.AppointedStaff.StaffID == staffId &&
                shift.StartTime < endTime &&
                shift.EndTime > startTime &&
                (shift.Status == ShiftStatus.SCHEDULED || shift.Status == ShiftStatus.ACTIVE);

            return shiftRepository.GetAllShifts().Any(IsWorkingDuring);
        }
    }
}
