using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.Services
{
    public class StaffAndShiftService
    {
        private readonly StaffRepository _staffRepo;
        private readonly ShiftRepository _shiftRepo;

        public StaffAndShiftService(StaffRepository staffRepo, ShiftRepository shiftRepo)
        {
            _staffRepo = staffRepo;
            _shiftRepo = shiftRepo;
        }

        public void SetShiftActive(int shiftId)
        {
            var shift = _shiftRepo.GetShifts().FirstOrDefault(s => s.Id == shiftId);
            if (shift != null)
            {
                _shiftRepo.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);
                _staffRepo.UpdateStaffAvailability(shift.AppointedStaff.StaffID, true, DoctorStatus.AVAILABLE);
            }
        }

        public void CancelShift(int shiftId)
        {
            var shift = _shiftRepo.GetShifts().FirstOrDefault(s => s.Id == shiftId);
            if (shift != null)
            {
                _staffRepo.UpdateStaffAvailability(shift.AppointedStaff.StaffID, false, DoctorStatus.OFF_DUTY);
                _shiftRepo.UpdateShiftStatus(shiftId, ShiftStatus.COMPLETED);
            }
        }

        public bool ValidateNoOverlap(int staffId, DateTime start, DateTime end)
        {
            return !_shiftRepo.GetShifts().Any(shift => (shift.AppointedStaff.StaffID == staffId) &&
                ((start >= shift.StartTime && start < shift.EndTime) || (end > shift.StartTime && end <= shift.EndTime)));
        }

        public void AddShift(Shift shift)
        {
            this._shiftRepo.AddShift(shift);
        }

        public List<Shift> GetDailyShifts(DateTime date)
        {
            return _shiftRepo.GetShifts().Where(shift => shift.StartTime.Date == date.Date).ToList();
        }

        public List<Shift> GetWeeklyShifts(DateTime date)
        {
            var monday = date.AddDays(-(int)DateTime.Now.DayOfWeek + (int)DayOfWeek.Monday);
            var sunday = monday.AddDays(7);
            return _shiftRepo.GetShifts().Where(shift => shift.StartTime >= monday && shift.StartTime < sunday).ToList();
        }

        public List<IStaff> FindStaffReplacements(Shift shift)
        {
            if (shift != null)
            {
                if (shift.AppointedStaff is Doctor doc)
                {
                    return _staffRepo.GetDoctorsBySpecialization(doc.Specialization)
                        .Where(doctor => doctor.Available && ValidateNoOverlap(doctor.StaffID, shift.StartTime, shift.EndTime))
                        .Cast<IStaff>()
                        .ToList();
                }
                else if (shift.AppointedStaff is Pharmacyst ph)
                {
                    return _staffRepo.GetPharmacystsByCertification(ph.Certification)
                        .Where(pharmacyst => pharmacyst.Available && ValidateNoOverlap(pharmacyst.StaffID, shift.StartTime, shift.EndTime))
                        .Cast<IStaff>()
                        .ToList();
                }
            }
            return new List<IStaff>();
        }

        public bool ValidateRestPeriod(int staffId, DateTime newShiftStart)
        {
            var lastShift = _shiftRepo.GetShiftsByStaffID(staffId).OrderByDescending(shift => shift.EndTime).FirstOrDefault();
            if (lastShift != null)
            {
                var restPeriod = (newShiftStart - lastShift.EndTime).TotalHours;
                return restPeriod >= 12; // Minimum rest period of 12 hours
            }
            return true; // If there are no previous shifts, the staff member had enough rest and we can assign the new shift

        }

        public List<IStaff> GetFilteredStaff(string location, string requiredSpecialization, string requiredCertification)
        {
            var availableStaff = this._staffRepo.GetAvailableStaff(requiredSpecialization, requiredCertification);

           

            if (location.Contains("Pharmacy", StringComparison.OrdinalIgnoreCase))
            {
                return availableStaff.Where(staff => staff is Pharmacyst).ToList();
            } else
            {
                return availableStaff.Where(staff => staff is Doctor doctor && doctor.Specialization.Equals(location, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public bool ReassignShift(Shift shift, IStaff newStaff)
        {
            var staffReplacements = FindStaffReplacements(shift);
            if (!staffReplacements.IsNullOrEmpty())
            {
                shift.AppointedStaff = staffReplacements.First();
                this._staffRepo.SaveStaffChanges();
                return true;
            } else
            {
                return false;
            }
        }
    }
}
        