using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        bool IsForStaffInRange(Shift shift) =>
            shift.AppointedStaff.StaffID == pharmacistStaffId
            && shift.StartTime < rangeEnd
            && shift.EndTime > rangeStart;
        DateTime ByStartTime(Shift shift) => shift.StartTime;

        IReadOnlyList<Shift> LoadAndFilter() => shiftRepository.GetAllShifts()
            .Where(IsForStaffInRange)
            .OrderBy(ByStartTime)
            .ToList();

        return Task.Run(LoadAndFilter);
    }

    public List<Pharmacyst> GetPharmacists() => staffRepository.GetPharmacists();
}
