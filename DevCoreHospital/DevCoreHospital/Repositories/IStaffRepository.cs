using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IStaffRepository
    {
        List<IStaff> LoadAllStaff();

        IStaff? GetStaffById(int staffId);
    }
}
