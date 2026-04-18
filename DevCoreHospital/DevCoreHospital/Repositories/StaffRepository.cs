using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.Repositories
{
    public class StaffRepository
    {
        private List<IStaff> cachedStaff;
        private readonly string connectionString;

        public StaffRepository(string connectionString)
        {
            this.connectionString = connectionString;
            cachedStaff = new List<IStaff>();
            LoadStaff();
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public void LoadStaff()
        {
            cachedStaff = FetchAllStaffFromDatabase();
        }

        public List<IStaff> LoadAllStaff()
        {
            return FetchAllStaffFromDatabase();
        }

        private List<IStaff> FetchAllStaffFromDatabase()
        {
            var allStaff = new List<IStaff>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT staff_id, role, first_name, last_name, contact_info,
                    is_available, license_number, specialization, status, certification, years_of_experience
                    FROM Staff", connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int staffId = reader.GetInt32(0);
                    string role = reader.GetString(1);
                    string firstName = reader.GetString(2);
                    string lastName = reader.GetString(3);
                    string contactInfo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    bool isAvailable = reader.GetBoolean(5);
                    string licenseNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                    string specialization = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                    string statusText = reader.IsDBNull(8) ? "Available" : reader.GetString(8);
                    string certification = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
                    int yearsOfExperience = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);

                    Enum.TryParse<DoctorStatus>(statusText, true, out DoctorStatus doctorStatus);

                    if (role == "Doctor")
                    {
                        allStaff.Add(new Doctor(staffId, firstName, lastName, contactInfo, string.Empty, isAvailable, specialization, licenseNumber, doctorStatus, yearsOfExperience));
                    }
                    else if (role == "Pharmacist")
                    {
                        allStaff.Add(new Pharmacyst(staffId, firstName, lastName, contactInfo, isAvailable, certification, yearsOfExperience));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetStaff: {ex.Message}");
            }
            return allStaff;
        }

        public void SaveStaffChanges()
        {
            SaveStaff(cachedStaff);
        }

        private void SaveStaff(List<IStaff> staffToSave)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                foreach (var staff in staffToSave)
                {
                    using var command = new SqlCommand(@"
                        UPDATE Staff SET
                            first_name = @FirstName,
                            last_name = @LastName,
                            contact_info = @ContactInfo,
                            is_available = @IsAvailable,
                            license_number = @License,
                            specialization = @Specialization,
                            status = @Status,
                            certification = @Certification
                        WHERE staff_id = @Id", connection);
                    AddParameter(command, "@FirstName", staff.FirstName);
                    AddParameter(command, "@LastName", staff.LastName);
                    AddParameter(command, "@ContactInfo", staff.ContactInfo);
                    AddParameter(command, "@IsAvailable", staff.Available);
                    AddParameter(command, "@Id", staff.StaffID);

                    if (staff is Doctor doctor)
                    {
                        AddParameter(command, "@License", doctor.LicenseNumber);
                        AddParameter(command, "@Specialization", doctor.Specialization);
                        AddParameter(command, "@Status", doctor.DoctorStatus.ToString());
                        AddParameter(command, "@Certification", DBNull.Value);
                    }
                    else if (staff is Pharmacyst pharmacist)
                    {
                        AddParameter(command, "@License", DBNull.Value);
                        AddParameter(command, "@Specialization", DBNull.Value);
                        AddParameter(command, "@Status", DBNull.Value);
                        AddParameter(command, "@Certification", pharmacist.Certification);
                    }
                    else
                    {
                        AddParameter(command, "@License", DBNull.Value);
                        AddParameter(command, "@Specialization", DBNull.Value);
                        AddParameter(command, "@Status", DBNull.Value);
                        AddParameter(command, "@Certification", DBNull.Value);
                    }
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error SaveStaff: {ex.Message}");
            }
        }

        public void UpdateStaff(IStaff staff)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    UPDATE Staff SET
                        first_name = @FirstName,
                        last_name = @LastName,
                        contact_info = @ContactInfo,
                        is_available = @IsAvailable,
                        license_number = @License,
                        specialization = @Specialization,
                        status = @Status,
                        certification = @Certification
                    WHERE staff_id = @Id", connection);
                AddParameter(command, "@FirstName", staff.FirstName);
                AddParameter(command, "@LastName", staff.LastName);
                AddParameter(command, "@ContactInfo", staff.ContactInfo);
                AddParameter(command, "@IsAvailable", staff.Available);
                AddParameter(command, "@Id", staff.StaffID);

                if (staff is Doctor doctor)
                {
                    AddParameter(command, "@License", doctor.LicenseNumber);
                    AddParameter(command, "@Specialization", doctor.Specialization);
                    AddParameter(command, "@Status", doctor.DoctorStatus.ToString());
                    AddParameter(command, "@Certification", DBNull.Value);
                }
                else if (staff is Pharmacyst pharmacist)
                {
                    AddParameter(command, "@License", DBNull.Value);
                    AddParameter(command, "@Specialization", DBNull.Value);
                    AddParameter(command, "@Status", DBNull.Value);
                    AddParameter(command, "@Certification", pharmacist.Certification);
                }
                else
                {
                    AddParameter(command, "@License", DBNull.Value);
                    AddParameter(command, "@Specialization", DBNull.Value);
                    AddParameter(command, "@Status", DBNull.Value);
                    AddParameter(command, "@Certification", DBNull.Value);
                }
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating staff: {ex.Message}");
            }
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var doctors = new List<(int, string)>();
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(@"
                SELECT staff_id,
                       LTRIM(RTRIM(CONCAT(COALESCE(first_name, ''), ' ', COALESCE(last_name, ''))))
                FROM Staff
                WHERE role = 'Doctor'
                ORDER BY first_name;", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                doctors.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }
            return doctors;
        }

        public IStaff? GetStaffById(int staffId)
        {
            return FetchAllStaffFromDatabase().FirstOrDefault(staffMember => staffMember.StaffID == staffId);
        }

        public List<Doctor> GetAvailableDoctors()
        {
            return FetchAllStaffFromDatabase().OfType<Doctor>().Where(doctor => doctor.Available).ToList();
        }

        private List<Pharmacyst> GetAvailablePharmacists()
        {
            return FetchAllStaffFromDatabase().OfType<Pharmacyst>().Where(pharmacist => pharmacist.Available).ToList();
        }

        public List<Pharmacyst> GetPharmacists()
        {
            return FetchAllStaffFromDatabase().OfType<Pharmacyst>().ToList();
        }

        private static string NormalizeForComparison(string? value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();

        public List<IStaff> GetPotentialSwapColleagues(IStaff requester)
        {
            var allStaff = FetchAllStaffFromDatabase();
            var requesterFromDatabase = allStaff.FirstOrDefault(staffMember => staffMember.StaffID == requester.StaffID);
            if (requesterFromDatabase == null)
            {
                return new List<IStaff>();
            }

            if (requesterFromDatabase is Doctor requesterDoctor)
            {
                var requesterSpecialization = NormalizeForComparison(requesterDoctor.Specialization);
                return allStaff
                    .OfType<Doctor>()
                    .Where(doctor =>
                        doctor.StaffID != requesterDoctor.StaffID &&
                        !string.IsNullOrWhiteSpace(doctor.Specialization) &&
                        NormalizeForComparison(doctor.Specialization) == requesterSpecialization)
                    .Cast<IStaff>()
                    .ToList();
            }

            if (requesterFromDatabase is Pharmacyst requesterPharmacist)
            {
                var requesterCertification = NormalizeForComparison(requesterPharmacist.Certification);
                return allStaff
                    .OfType<Pharmacyst>()
                    .Where(pharmacist =>
                        pharmacist.StaffID != requesterPharmacist.StaffID &&
                        !string.IsNullOrWhiteSpace(pharmacist.Certification) &&
                        NormalizeForComparison(pharmacist.Certification) == requesterCertification)
                    .Cast<IStaff>()
                    .ToList();
            }

            return new List<IStaff>();
        }

        public List<IStaff> GetAvailableStaff(string doctorSpecialization, string pharmacistCertification)
        {
            var availableDoctors = GetAvailableDoctors();
            var availablePharmacists = GetAvailablePharmacists();
            var availableStaff = new List<IStaff>();

            if (!string.IsNullOrEmpty(doctorSpecialization) && !string.IsNullOrEmpty(pharmacistCertification))
            {
                availableStaff.AddRange(availableDoctors.Where(doctor => doctor.Specialization.Equals(doctorSpecialization, StringComparison.OrdinalIgnoreCase)));
                availableStaff.AddRange(availablePharmacists.Where(pharmacist => pharmacist.Certification.Equals(pharmacistCertification, StringComparison.OrdinalIgnoreCase)));
            }
            else if (!doctorSpecialization.IsNullOrEmpty())
            {
                availableStaff.AddRange(availableDoctors.Where(doctor => doctor.Specialization.Equals(doctorSpecialization, StringComparison.OrdinalIgnoreCase)));
            }
            else if (!pharmacistCertification.IsNullOrEmpty())
            {
                availableStaff.AddRange(availablePharmacists.Where(pharmacist => pharmacist.Certification.Equals(pharmacistCertification, StringComparison.OrdinalIgnoreCase)));
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
            return FetchAllStaffFromDatabase().OfType<Doctor>()
                .Where(doctor => doctor.Specialization.Equals(specialization, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public List<Pharmacyst> GetPharmacystsByCertification(string certification)
        {
            return FetchAllStaffFromDatabase().OfType<Pharmacyst>()
                .Where(pharmacist => pharmacist.Certification.Equals(certification, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void UpdateStaffAvailability(int staffId, bool isAvailable, DoctorStatus status = DoctorStatus.OFF_DUTY)
        {
            var staff = cachedStaff.FirstOrDefault(staffMember => staffMember.StaffID == staffId);
            if (staff != null)
            {
                staff.Available = isAvailable;
                if (staff is Doctor doctor)
                {
                    doctor.DoctorStatus = status;
                }
                UpdateStaff(staff);
            }
        }
    }
}
