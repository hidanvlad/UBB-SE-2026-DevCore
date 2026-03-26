using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services;

public sealed class PharmacyScheduleService : IPharmacyScheduleService
{
    private readonly IStaffRepository _staffRepo;
    private readonly IShiftRepository _shiftRepo;

    public PharmacyScheduleService(IStaffRepository staffRepo, IShiftRepository shiftRepo)
    {
        _staffRepo = staffRepo;
        _shiftRepo = shiftRepo;
    }

    public Task<IReadOnlyList<PharmacyShift>> GetShiftsAsync(string pharmacistStaffId, DateTime rangeStart, DateTime rangeEnd)
    {
        var staff = _staffRepo.FindByStaffCode(pharmacistStaffId);
        if (staff == null)
            return Task.FromResult<IReadOnlyList<PharmacyShift>>(Array.Empty<PharmacyShift>());

        var shifts = _shiftRepo.GetShiftsForStaffInRange(staff.Id, rangeStart, rangeEnd);
        var list = shifts.Select(s => MapToPharmacyShift(s, staff.StaffCode)).ToList();
        return Task.FromResult<IReadOnlyList<PharmacyShift>>(list);
    }

    private static PharmacyShift MapToPharmacyShift(Shift shift, string pharmacistStaffCode)
    {
        return new PharmacyShift
        {
            Id = $"PS-{shift.Id}",
            PharmacistStaffId = pharmacistStaffCode,
            RotationAssignment = shift.RotationAssignment,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Status = shift.Status.ToString()
        };
    }
}
