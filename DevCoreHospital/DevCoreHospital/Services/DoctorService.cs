using DevCoreHospital.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class DoctorService
    {
        public Task<List<DoctorItem>> GetDoctorsAsync()
        {
            // Replace later with DB call when your data layer is ready
            var data = new List<DoctorItem>
            {
                new DoctorItem { Id = 1, FullName = "Dr. Andrei Popescu" },
                new DoctorItem { Id = 2, FullName = "Dr. Maria Ionescu" },
                new DoctorItem { Id = 3, FullName = "Dr. Vlad Georgescu" }
            };

            return Task.FromResult(data);
        }
    }
}