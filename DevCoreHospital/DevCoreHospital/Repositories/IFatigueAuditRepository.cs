using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IFatigueAuditRepository
    {
        IReadOnlyList<RosterShift> GetAllShifts();
        IReadOnlyList<StaffProfile> GetStaffProfiles();
        int UpdateShiftStaffId(int shiftId, int newStaffId);
    }
}
