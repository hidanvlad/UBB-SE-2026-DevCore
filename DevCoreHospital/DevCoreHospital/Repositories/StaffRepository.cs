using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Data;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.Repositories
{
    public class StaffRepository
    {
        private List<IStaff> _staffList;
        private readonly DatabaseManager _dbManager;

        public StaffRepository(DatabaseManager dbManager)
        {
            _staffList = new List<IStaff>();
            _dbManager = dbManager;
            LoadStaff();
        }

        public void LoadStaff()
        {
            _staffList = _dbManager.GetStaff();
        }

        public List<IStaff> LoadAllStaff()
        {
            return _dbManager.GetStaff();
        }

        public void SaveStaffChanges()
        {
            _dbManager.SaveStaff(_staffList);
        }

        public IStaff? GetStaffById(int staffId)
        {
            return _staffList.FirstOrDefault(s => s.StaffID == staffId);
        }

        public List<Doctor> GetAvailableDoctors()
        {
            return _dbManager.GetStaff().OfType<Doctor>().Where(doctor => doctor.Available).ToList();
        }

        private List<Pharmacyst> GetAvailablePharmacists()
        {
            return _dbManager.GetStaff().OfType<Pharmacyst>().Where(ph => ph.Available).ToList();
        }

        public List<Pharmacyst> GetPharmacists()
        {
            return _dbManager.GetStaff().OfType<Pharmacyst>().ToList();
        }

        public List<IStaff> GetPotentialSwapColleagues(IStaff requester)
        {
            LoadStaff();

            if (requester is Doctor requesterDoctor)
            {
                return _staffList
                    .OfType<Doctor>()
                    .Where(d =>
                        d.StaffID != requester.StaffID &&
                        d.Available == true &&
                        d.Specialization.Equals(requesterDoctor.Specialization, StringComparison.OrdinalIgnoreCase))
                    .Cast<IStaff>()
                    .ToList();
            }

            if (requester is Pharmacyst requesterPharmacyst)
            {
                return _staffList
                    .OfType<Pharmacyst>()
                    .Where(p =>
                        p.StaffID != requester.StaffID &&
                        p.Available == true &&
                        p.Certification.Equals(requesterPharmacyst.Certification, StringComparison.OrdinalIgnoreCase))
                    .Cast<IStaff>()
                    .ToList();
            }

            return new List<IStaff>();
        }

        public List<IStaff> GetAvailableStaff(string doctorSpecialization, string pharmacystCertification)
        {
            var availableDoctors = GetAvailableDoctors();
            var availablePharmacists = GetAvailablePharmacists();
            var availableStaff = new List<IStaff>();

            if (!string.IsNullOrEmpty(doctorSpecialization) && !string.IsNullOrEmpty(pharmacystCertification))
            {
                var filteredDoctors = availableDoctors.Where(doctor => doctor.Specialization.Equals(doctorSpecialization, StringComparison.OrdinalIgnoreCase));
                var filteredPharmacists = availablePharmacists.Where(ph => ph.Certification.Equals(pharmacystCertification, StringComparison.OrdinalIgnoreCase));
                availableStaff.AddRange(filteredDoctors);
                availableStaff.AddRange(filteredPharmacists);
            }
            else if (!doctorSpecialization.IsNullOrEmpty())
            {
                var filteredDoctors = availableDoctors.Where(doctor => doctor.Specialization.Equals(doctorSpecialization, StringComparison.OrdinalIgnoreCase));
                availableStaff.AddRange(filteredDoctors);
            }
            else if (!pharmacystCertification.IsNullOrEmpty())
            {
                var filteredPharmacists = availablePharmacists.Where(ph => ph.Certification.Equals(pharmacystCertification, StringComparison.OrdinalIgnoreCase));
                availableStaff.AddRange(filteredPharmacists);
            }
            else
            {
                availableStaff.AddRange(availableDoctors);
                availableStaff.AddRange(availablePharmacists);
            }

            return availableStaff;
        }

        public List<Doctor> GetDoctorsBySpecialization(string specialization)
        {
            return _dbManager.GetStaff()
                .OfType<Doctor>()
                .Where(doctor => doctor.Specialization.Equals(specialization, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public List<Pharmacyst> GetPharmacystsByCertification(string certification)
        {
            return _dbManager.GetStaff()
                .OfType<Pharmacyst>()
                .Where(ph => ph.Certification.Equals(certification, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void UpdateStaffAvailability(int staffId, bool isAvailable, DoctorStatus status = DoctorStatus.OFF_DUTY)
        {
            var staff = _staffList.FirstOrDefault(st => st.StaffID == staffId);
            if (staff != null)
            {
                staff.Available = isAvailable;
                if (staff is Doctor doc) doc.DoctorStatus = status;
                _dbManager.UpdateStaff(staff);
            }
        }
    }
}