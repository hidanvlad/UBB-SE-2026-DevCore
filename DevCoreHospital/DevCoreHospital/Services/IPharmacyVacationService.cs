using System.Collections.Generic;
using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IPharmacyVacationService
    {
        IReadOnlyList<Pharmacyst> GetPharmacists();

        void RegisterVacation(int pharmacistStaffId, DateTime startDate, DateTime endDate);
    }
}
