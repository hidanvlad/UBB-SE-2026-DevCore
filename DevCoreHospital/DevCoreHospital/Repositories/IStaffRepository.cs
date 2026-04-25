using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IStaffRepository
    {
        List<IStaff> LoadAllStaff();

        IStaff? GetStaffById(int staffId);

        Task<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>> GetAllDoctorsAsync();

        Task UpdateStatusAsync(int staffId, string status);
    }
}
