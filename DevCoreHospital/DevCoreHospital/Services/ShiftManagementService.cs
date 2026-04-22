using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class ShiftManagementService : IShiftManagementService
    {
        private readonly IShiftManagementStaffRepository staffRepository;
        private readonly IShiftManagementShiftRepository shiftRepository;

        public ShiftManagementService(IShiftManagementStaffRepository staffRepository, IShiftManagementShiftRepository shiftRepository)
        {
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public void SetShiftActive(int shiftId)
        {
            var shift = shiftRepository.GetShifts().FirstOrDefault(existingShift => existingShift.Id == shiftId);
            if (shift != null)
            {
                shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);
                staffRepository.UpdateStaffAvailability(shift.AppointedStaff.StaffID, true, DoctorStatus.AVAILABLE);
            }
        }

        public void CancelShift(int shiftId)
        {
            var shift = shiftRepository.GetShifts().FirstOrDefault(existingShift => existingShift.Id == shiftId);
            if (shift != null)
            {
                staffRepository.UpdateStaffAvailability(shift.AppointedStaff.StaffID, false, DoctorStatus.OFF_DUTY);
                shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.COMPLETED);
            }
        }

        public bool ValidateNoOverlap(int staffId, DateTime start, DateTime end)
        {
            return !shiftRepository.GetShifts().Any(shift =>
                (shift.AppointedStaff.StaffID == staffId) &&
                ((start >= shift.StartTime && start < shift.EndTime) || (end > shift.StartTime && end <= shift.EndTime)));
        }

        public void AddShift(Shift shift) => shiftRepository.AddShift(shift);

        public List<Shift> GetDailyShifts(DateTime date)
            => shiftRepository.GetShifts().Where(shift => shift.StartTime.Date == date.Date).ToList();

        public List<Shift> GetWeeklyShifts(DateTime date)
        {
            var weekStart = date.AddDays(-(int)DateTime.Now.DayOfWeek + (int)DayOfWeek.Monday);
            var weekEnd = weekStart.AddDays(7);
            return shiftRepository.GetShifts().Where(shift => shift.StartTime >= weekStart && shift.StartTime < weekEnd).ToList();
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

            if (location.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
            {
                filteredStaff.AddRange(allStaff.OfType<Pharmacyst>()
                    .Where(pharmacist => pharmacist.Certification.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                filteredStaff.AddRange(allStaff.OfType<Doctor>()
                    .Where(doctor => doctor.Specialization.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase)));
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

            return allStaff.Where(staffMember =>
                staffMember.GetType() == currentStaff.GetType() &&
                staffMember.StaffID != currentStaff.StaffID &&
                ValidateNoOverlap(staffMember.StaffID, shift.StartTime, shift.EndTime))
                .ToList();
        }

        public List<string> GetSpecializationsAndCertificationsForLocation(string location)
        {
            var qualificationNames = new List<string>();
            var allStaff = staffRepository.LoadAllStaff();

            if (location.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
            {
                qualificationNames.AddRange(allStaff.OfType<Pharmacyst>()
                    .Where(pharmacist => !string.IsNullOrEmpty(pharmacist.Certification))
                    .Select(pharmacist => pharmacist.Certification));
            }
            else
            {
                qualificationNames.AddRange(allStaff.OfType<Doctor>()
                    .Where(doctor => !string.IsNullOrEmpty(doctor.Specialization))
                    .Select(doctor => doctor.Specialization));
            }

            return qualificationNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
        }
    }
}
