using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IPharmacyShiftRepository
    {
        IReadOnlyList<Shift> GetAllShifts();
        void AddShift(Shift shift);
    }
}
