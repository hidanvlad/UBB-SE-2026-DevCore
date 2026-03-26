using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Data;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories;

public sealed class ShiftRepository : IShiftRepository
{
    private readonly List<Shift> _shiftList;
    private readonly DatabaseManager _dbManager;

    public ShiftRepository(DatabaseManager dbManager)
    {
        _dbManager = dbManager;
        _shiftList = new List<Shift>();
        LoadShifts();
    }

    private void LoadShifts()
    {
        _ = _dbManager.ConnectionFactory;

        if (_shiftList.Count > 0)
            return;

        const int staffId = 1;
        var today = DateTime.Today;

        AddShift(new Shift
        {
            Id = 1,
            StaffId = staffId,
            RotationAssignment = "Main dispensary — controlled substances audit",
            StartTime = today.AddHours(7),
            EndTime = today.AddHours(15),
            Status = ShiftStatus.COMPLETED
        });

        AddShift(new Shift
        {
            Id = 2,
            StaffId = staffId,
            RotationAssignment = "Outpatient clinic satellite",
            StartTime = today.AddHours(10),
            EndTime = today.AddHours(18),
            Status = ShiftStatus.ACTIVE
        });

        AddShift(new Shift
        {
            Id = 3,
            StaffId = staffId,
            RotationAssignment = "Oncology infusion suite",
            StartTime = today.AddDays(1).AddHours(8),
            EndTime = today.AddDays(1).AddHours(16),
            Status = ShiftStatus.SCHEDULED
        });

        AddShift(new Shift
        {
            Id = 4,
            StaffId = staffId,
            RotationAssignment = "Central pharmacy — IV admixture",
            StartTime = today.AddDays(2).AddHours(6),
            EndTime = today.AddDays(2).AddHours(14),
            Status = ShiftStatus.SCHEDULED
        });

        AddShift(new Shift
        {
            Id = 5,
            StaffId = staffId,
            RotationAssignment = "Emergency department",
            StartTime = today.AddDays(3).AddHours(14),
            EndTime = today.AddDays(3).AddHours(22),
            Status = ShiftStatus.SCHEDULED
        });
    }

    private void AddShift(Shift shift)
    {
        if (shift.Id == 0)
            shift.Id = _shiftList.Count == 0 ? 1 : _shiftList.Max(s => s.Id) + 1;

        _shiftList.Add(shift);
    }

    private void CancelShift(int shiftId)
    {
        var shift = _shiftList.FirstOrDefault(s => s.Id == shiftId);
        if (shift != null)
            shift.Status = ShiftStatus.CANCELLED;
    }

    private Shift? GetShiftByStaff(int staffId)
    {
        return _shiftList.FirstOrDefault(s =>
            s.StaffId == staffId &&
            s.Status == ShiftStatus.ACTIVE);
    }

    private List<Shift> GetActiveShifts()
    {
        return _shiftList
            .Where(s => s.Status == ShiftStatus.ACTIVE)
            .ToList();
    }

    public float GetWeeklyHours(int staffId)
    {
        var weekStart = StartOfWeek(DateTime.Today);
        var weekEnd = weekStart.AddDays(7);

        float hours = 0;
        foreach (var shift in _shiftList.Where(s => s.StaffId == staffId))
        {
            if (shift.StartTime < weekStart || shift.StartTime >= weekEnd)
                continue;

            var end = shift.EndTime ?? shift.StartTime;
            hours += (float)(end - shift.StartTime).TotalHours;
        }

        return hours;
    }

    public IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime rangeStart, DateTime rangeEnd)
    {
        return _shiftList
            .Where(s => s.StaffId == staffId && s.StartTime >= rangeStart && s.StartTime < rangeEnd)
            .OrderBy(s => s.StartTime)
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
}
