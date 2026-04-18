using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface ISalaryComputationService
    {
        Task<double> ComputeSalaryDoctorAsync(Doctor doctor, List<Shift> monthlyShifts, int month, int year);
        Task<double> ComputeSalaryPharmacistAsync(Pharmacyst pharmacist, List<Shift> monthlyShifts, int month, int year);
    }
}
