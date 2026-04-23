using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IPharmacyShiftRepository
    {
        List<Shift> GetShifts();
        List<Shift> GetShiftsByStaffID(int staffId);
        void AddShift(Shift shift);
    }
}
