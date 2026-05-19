using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IPharmacyStaffRepository
    {
        List<Pharmacyst> GetPharmacists();
    }
}
