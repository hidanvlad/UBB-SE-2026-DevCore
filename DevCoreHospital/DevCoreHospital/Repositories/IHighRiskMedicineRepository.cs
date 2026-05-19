using System.Collections.Generic;

namespace DevCoreHospital.Repositories
{
    public interface IHighRiskMedicineRepository
    {
        IReadOnlyList<(string MedicineName, string WarningMessage)> GetAllHighRiskMedicines();
    }
}
