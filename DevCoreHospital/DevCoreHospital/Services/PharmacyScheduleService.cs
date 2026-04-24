using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services;

public sealed class PharmacyScheduleService : IPharmacyScheduleService
{
    private readonly IShiftRepository shiftRepo;
    private readonly IPharmacyStaffRepository staffRepo;

    public PharmacyScheduleService(IShiftRepository shiftRepo, IPharmacyStaffRepository staffRepo)
    {
        this.shiftRepo = shiftRepo;
        this.staffRepo = staffRepo;
    }

    public Task<IReadOnlyList<Shift>> GetShiftsAsync(int pharmacistStaffId, DateTime rangeStart, DateTime rangeEnd)
    {
        return Task.Run<IReadOnlyList<Shift>>(
            () => shiftRepo.GetShiftsForStaffInRange(pharmacistStaffId, rangeStart, rangeEnd));
    }

    public List<Pharmacyst> GetPharmacists() => staffRepo.GetPharmacists();
}
