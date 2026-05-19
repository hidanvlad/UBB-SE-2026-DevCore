using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class StaffRepository : IShiftManagementStaffRepository, IStaffRepository, IPharmacyStaffRepository
    {
        private const string DoctorRoleLabel = "Doctor";
        private const string PharmacistRoleLabel = "Pharmacist";
        private const string DefaultDoctorStatusLabel = "Available";

        private readonly string connectionString;

        public StaffRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<IStaff> LoadAllStaff()
        {
            var allStaff = new List<IStaff>();

            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                SELECT staff_id, role, first_name, last_name, contact_info,
                       is_available, license_number, specialization, status,
                       certification, years_of_experience
                FROM Staff;", connection);

            using SqlDataReader reader = command.ExecuteReader();
            int staffIdOrdinal = reader.GetOrdinal("staff_id");
            int roleOrdinal = reader.GetOrdinal("role");
            int firstNameOrdinal = reader.GetOrdinal("first_name");
            int lastNameOrdinal = reader.GetOrdinal("last_name");
            int contactInfoOrdinal = reader.GetOrdinal("contact_info");
            int isAvailableOrdinal = reader.GetOrdinal("is_available");
            int licenseNumberOrdinal = reader.GetOrdinal("license_number");
            int specializationOrdinal = reader.GetOrdinal("specialization");
            int statusOrdinal = reader.GetOrdinal("status");
            int certificationOrdinal = reader.GetOrdinal("certification");
            int yearsOfExperienceOrdinal = reader.GetOrdinal("years_of_experience");

            while (reader.Read())
            {
                int staffId = reader.GetInt32(staffIdOrdinal);
                string role = reader.GetString(roleOrdinal);
                string firstName = reader.GetString(firstNameOrdinal);
                string lastName = reader.GetString(lastNameOrdinal);
                string contactInfo = reader.IsDBNull(contactInfoOrdinal) ? string.Empty : reader.GetString(contactInfoOrdinal);
                bool isAvailable = reader.GetBoolean(isAvailableOrdinal);
                string licenseNumber = reader.IsDBNull(licenseNumberOrdinal) ? string.Empty : reader.GetString(licenseNumberOrdinal);
                string specialization = reader.IsDBNull(specializationOrdinal) ? string.Empty : reader.GetString(specializationOrdinal);
                string statusText = reader.IsDBNull(statusOrdinal) ? DefaultDoctorStatusLabel : reader.GetString(statusOrdinal);
                string certification = reader.IsDBNull(certificationOrdinal) ? string.Empty : reader.GetString(certificationOrdinal);
                int yearsOfExperience = reader.IsDBNull(yearsOfExperienceOrdinal) ? 0 : reader.GetInt32(yearsOfExperienceOrdinal);

                Enum.TryParse<DoctorStatus>(statusText, true, out DoctorStatus doctorStatus);

                if (role == DoctorRoleLabel)
                {
                    allStaff.Add(new Doctor(staffId, firstName, lastName, contactInfo,
                        isAvailable, specialization, licenseNumber, doctorStatus, yearsOfExperience));
                }
                else if (role == PharmacistRoleLabel)
                {
                    allStaff.Add(new Pharmacyst(staffId, firstName, lastName, contactInfo,
                        isAvailable, certification, yearsOfExperience));
                }
            }
            return allStaff;
        }

        public IStaff? GetStaffById(int staffId)
        {
            bool HasMatchingId(IStaff staffMember) => staffMember.StaffID == staffId;
            return LoadAllStaff().FirstOrDefault(HasMatchingId);
        }

        public List<Pharmacyst> GetPharmacists() => LoadAllStaff().OfType<Pharmacyst>().ToList();

        public async Task<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>> GetAllDoctorsAsync()
        {
            var doctors = new List<(int, string, string)>();
            using SqlConnection connection = GetConnection();
            await connection.OpenAsync();
            using SqlCommand command = new SqlCommand(@"
                SELECT staff_id, first_name, last_name
                FROM Staff
                WHERE role = @DoctorRole;", connection);
            AddParameter(command, "@DoctorRole", DoctorRoleLabel);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            int staffIdOrdinal = reader.GetOrdinal("staff_id");
            int firstNameOrdinal = reader.GetOrdinal("first_name");
            int lastNameOrdinal = reader.GetOrdinal("last_name");
            while (await reader.ReadAsync())
            {
                doctors.Add((
                    reader.GetInt32(staffIdOrdinal),
                    reader.IsDBNull(firstNameOrdinal) ? string.Empty : reader.GetString(firstNameOrdinal),
                    reader.IsDBNull(lastNameOrdinal) ? string.Empty : reader.GetString(lastNameOrdinal)));
            }
            return doctors;
        }

        public async Task UpdateStatusAsync(int staffId, string status)
        {
            using SqlConnection connection = GetConnection();
            await connection.OpenAsync();
            using SqlCommand command = new SqlCommand(
                "UPDATE Staff SET status = @Status WHERE staff_id = @StaffId;", connection);
            AddParameter(command, "@Status", status);
            AddParameter(command, "@StaffId", staffId);
            await command.ExecuteNonQueryAsync();
        }

        public void UpdateStaffAvailability(int staffId, bool isAvailable, DoctorStatus status = DoctorStatus.OFF_DUTY)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "UPDATE Staff SET is_available = @IsAvailable, status = @Status WHERE staff_id = @StaffId;", connection);
            AddParameter(command, "@IsAvailable", isAvailable);
            AddParameter(command, "@Status", status.ToString());
            AddParameter(command, "@StaffId", staffId);
            command.ExecuteNonQuery();
        }

        public void UpdateStaff(IStaff staff)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                UPDATE Staff SET
                    first_name = @FirstName, last_name = @LastName,
                    contact_info = @ContactInfo, is_available = @IsAvailable,
                    license_number = @LicenseNumber, specialization = @Specialization,
                    status = @Status, certification = @Certification
                WHERE staff_id = @StaffId;", connection);
            AddParameter(command, "@FirstName", staff.FirstName);
            AddParameter(command, "@LastName", staff.LastName);
            AddParameter(command, "@ContactInfo", staff.ContactInfo);
            AddParameter(command, "@IsAvailable", staff.Available);
            AddParameter(command, "@StaffId", staff.StaffID);

            if (staff is Doctor doctor)
            {
                AddParameter(command, "@LicenseNumber", doctor.LicenseNumber);
                AddParameter(command, "@Specialization", doctor.Specialization);
                AddParameter(command, "@Status", doctor.DoctorStatus.ToString());
                AddParameter(command, "@Certification", DBNull.Value);
            }
            else if (staff is Pharmacyst pharmacist)
            {
                AddParameter(command, "@LicenseNumber", DBNull.Value);
                AddParameter(command, "@Specialization", DBNull.Value);
                AddParameter(command, "@Status", DBNull.Value);
                AddParameter(command, "@Certification", pharmacist.Certification);
            }
            else
            {
                AddParameter(command, "@LicenseNumber", DBNull.Value);
                AddParameter(command, "@Specialization", DBNull.Value);
                AddParameter(command, "@Status", DBNull.Value);
                AddParameter(command, "@Certification", DBNull.Value);
            }

            command.ExecuteNonQuery();
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
