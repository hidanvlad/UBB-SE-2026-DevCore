using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IPharmacyVacationService
    {
        IReadOnlyList<Pharmacyst> GetPharmacists();

        /// <summary>
        /// Registers a vacation shift for the pharmacist. Throws InvalidOperationException
        /// on business-rule violations (overlapping shift, monthly vacation-day limit exceeded)
        /// and ArgumentException on invalid input.
        /// </summary>
        void RegisterVacation(int pharmacistStaffId, DateTime startDate, DateTime endDate);
    }
}
