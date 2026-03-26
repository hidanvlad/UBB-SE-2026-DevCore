using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories;

public interface IStaffRepository
{
    Doctor? GetDoctorBySpecialization(string spec);

    Staff? FindByStaffCode(string staffCode);
}
