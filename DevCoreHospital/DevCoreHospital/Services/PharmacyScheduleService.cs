using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services;

public sealed class PharmacyScheduleService : IPharmacyScheduleService
{
    private readonly IShiftRepository _shiftRepo;

    public PharmacyScheduleService(IShiftRepository shiftRepo)
    {
        _shiftRepo = shiftRepo;
    }

    public Task<IReadOnlyList<Shift>> GetShiftsAsync(int pharmacistStaffId, DateTime rangeStart, DateTime rangeEnd)
    {
        var shifts = _shiftRepo.GetShiftsForStaffInRange(pharmacistStaffId, rangeStart, rangeEnd);
        return Task.FromResult<IReadOnlyList<Shift>>(shifts);
    }
}
