using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services;

public sealed class PharmacyScheduleService : IPharmacyScheduleService
{
    private readonly IShiftRepository shiftRepository;
    private readonly IPharmacyStaffRepository staffRepository;

    public PharmacyScheduleService(IShiftRepository shiftRepository, IPharmacyStaffRepository staffRepository)
    {
        this.shiftRepository = shiftRepository;
        this.staffRepository = staffRepository;
    }

    public Task<IReadOnlyList<Shift>> GetShiftsAsync(int pharmacistStaffId, DateTime rangeStart, DateTime rangeEnd)
    {
        return Task.Run<IReadOnlyList<Shift>>(
            () => shiftRepository.GetShiftsForStaffInRange(pharmacistStaffId, rangeStart, rangeEnd));
    }

    public List<Pharmacyst> GetPharmacists() => staffRepository.GetPharmacists();
}
