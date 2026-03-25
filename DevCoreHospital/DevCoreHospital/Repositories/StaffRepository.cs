using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Data;

namespace DevCoreHospital.Repositories
{
    public class StaffRepository
    {
        private List<Staff> staffList;
        private DatabaseManager dbManager;

        public StaffRepository(DatabaseManager dbManager)
        {
            this.staffList = new List<Staff>();
            this.dbManager = dbManager;
        }
        public List<Doctor> GetAvailableDoctors(string specialization)
        {
            return staffList.OfType<Doctor>()
                .Where(d => d.Specialization == specialization)
                .ToList();
        }

        public void UpdateStaffAvailability(int staffId, bool isAvailable, string status = "")
        {
            var staff = staffList.FirstOrDefault(s => s.Id == staffId);
            if (staff != null)
            {
                staff.IsAvailable = isAvailable;
                if (staff is Doctor doc && !string.IsNullOrEmpty(status)) doc.DoctorStatus = status;
            }
        }
    }
}
