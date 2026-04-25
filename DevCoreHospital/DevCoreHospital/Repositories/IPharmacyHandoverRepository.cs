using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IPharmacyHandoverRepository
    {
        IReadOnlyList<PharmacyHandover> GetAllPharmacyHandovers();
    }
}
