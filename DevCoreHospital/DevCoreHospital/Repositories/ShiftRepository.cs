using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Data;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public class ShiftRepository : IShiftRepository
    {
        private List<Shift> _shiftList;
        private readonly DatabaseManager _dbManager;

        public ShiftRepository(DatabaseManager dbManager)
        {
            _shiftList = new List<Shift>();
            _dbManager = dbManager;
            _shiftList = _dbManager.GetShifts();
        }

        public void AddShift(Shift newShift)
        {
            _shiftList.Add(newShift);
            _dbManager.AddNewShift(newShift);
        }

        public void CancelShift(int shiftId)
        {
            var shiftToCancel = _shiftList.FirstOrDefault(shift => shift.Id == shiftId);
            if (shiftToCancel != null)
            {
                _shiftList.Remove(shiftToCancel);
                _dbManager.DeleteShift(shiftId);
            }
        }

        public List<Shift> GetShifts() => _shiftList;

        public Shift? GetShiftById(int shiftId)
            => _shiftList.FirstOrDefault(shift => shift.Id == shiftId);

        public List<Shift> GetShiftsByStaffID(int staffId)
            => _shiftList.Where(shift => shift.AppointedStaff.StaffID == staffId).ToList();

        public List<Shift> GetActiveShifts()
            => _shiftList.Where(shift => shift.Status == ShiftStatus.ACTIVE).ToList();

        // REQUIRED by IShiftRepository (fix CS0535)
        public float GetWeeklyHours(int staffId)
        {
            var shifts = GetShiftsByStaffID(staffId);
            float totalHours = 0;

            var now = DateTime.Now;
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = now.Date.AddDays(-diff);
            var nextMonday = monday.AddDays(7);

            foreach (var shift in shifts)
            {
                if (shift.StartTime >= monday && shift.StartTime < nextMonday)
                {
                    totalHours += (float)(shift.EndTime - shift.StartTime).TotalHours;
                }
            }

            return totalHours;
        }

        public IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime rangeStart, DateTime rangeEnd)
        {
            return _dbManager
                .GetShifts()
                .Where(shift =>
                    shift.AppointedStaff.StaffID == staffId &&
                    shift.StartTime < rangeEnd &&
                    shift.EndTime > rangeStart)
                .OrderBy(shift => shift.StartTime)
                .ToList();
        }

        public bool IsStaffWorkingDuring(int staffId, DateTime start, DateTime end)
            => _dbManager.IsStaffWorkingDuring(staffId, start, end);

        public void UpdateShiftStatus(int shiftId, ShiftStatus status)
        {
            var shiftToUpdate = _shiftList.FirstOrDefault(shift => shift.Id == shiftId);
            if (shiftToUpdate != null)
            {
                shiftToUpdate.Status = status;
                _dbManager.UpdateShift(shiftToUpdate);
            }
        }

        public void Refresh()
        {
            _shiftList = _dbManager.GetShifts();
        }
    }
}