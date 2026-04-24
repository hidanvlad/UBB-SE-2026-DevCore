using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.Repositories
{
    public class StaffRepository : IShiftManagementStaffRepository, IStaffRepository, IPharmacyStaffRepository
    {
        private readonly string connectionString;

        public StaffRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        // ── Core fetch ──────────────────────────────────────────────────────
        public List<IStaff> LoadAllStaff() => FetchAllStaffFromDatabase();

        private List<IStaff> FetchAllStaffFromDatabase()
        {
            var allStaff = new List<IStaff>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT staff_id, role, first_name, last_name, contact_info,
                           is_available, license_number, specialization, status,
                           certification, years_of_experience
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
                    int yearsOfExp = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);

                    Enum.TryParse<DoctorStatus>(statusText, true, out DoctorStatus doctorStatus);

                    if (role == "Doctor")
                    {
                        allStaff.Add(new Doctor(staffId, firstName, lastName, contactInfo,
                            string.Empty, isAvailable, specialization, licenseNumber, doctorStatus, yearsOfExp));
                    }
                    else if (role == "Pharmacist")
                    {
                        allStaff.Add(new Pharmacyst(staffId, firstName, lastName, contactInfo,
                            isAvailable, certification, yearsOfExp));
                    }
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error FetchAllStaffFromDatabase: {exception.Message}");
            }
            return allStaff;
        }

        // ── Read methods ────────────────────────────────────────────────────
        public IStaff? GetStaffById(int staffId)
        {
            bool HasMatchingId(IStaff staffMember) => staffMember.StaffID == staffId;
            return FetchAllStaffFromDatabase().FirstOrDefault(HasMatchingId);
        }

        public List<Doctor> GetAvailableDoctors()
        {
            bool IsAvailable(Doctor doctor) => doctor.Available;
            return FetchAllStaffFromDatabase().OfType<Doctor>().Where(IsAvailable).ToList();
        }

        public List<Pharmacyst> GetPharmacists()
            => FetchAllStaffFromDatabase().OfType<Pharmacyst>().ToList();

        private List<Pharmacyst> GetAvailablePharmacists()
        {
            bool IsAvailable(Pharmacyst pharmacist) => pharmacist.Available;
            return FetchAllStaffFromDatabase().OfType<Pharmacyst>().Where(IsAvailable).ToList();
        }

        public List<Doctor> GetDoctorsBySpecialization(string specialization)
        {
            bool HasMatchingSpecialization(Doctor doctor) => doctor.Specialization.Equals(specialization, StringComparison.OrdinalIgnoreCase);
            return FetchAllStaffFromDatabase().OfType<Doctor>()
                .Where(HasMatchingSpecialization)
                .ToList();
        }

        public List<Pharmacyst> GetPharmacystsByCertification(string certification)
        {
            bool HasMatchingCertification(Pharmacyst pharmacist) => pharmacist.Certification.Equals(certification, StringComparison.OrdinalIgnoreCase);
            return FetchAllStaffFromDatabase().OfType<Pharmacyst>()
                .Where(HasMatchingCertification)
                .ToList();
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

        // ── Write methods ───────────────────────────────────────────────────
        public void UpdateStaffAvailability(int staffId, bool isAvailable, DoctorStatus status = DoctorStatus.OFF_DUTY)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(
                    "UPDATE Staff SET is_available = @IsAvailable, status = @Status WHERE staff_id = @Id",
                    connection);
                AddParameter(command, "@IsAvailable", isAvailable);
                AddParameter(command, "@Status",      status.ToString());
                AddParameter(command, "@Id",           staffId);
                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error UpdateStaffAvailability: {exception.Message}");
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
                        first_name = @FirstName, last_name = @LastName,
                        contact_info = @ContactInfo, is_available = @IsAvailable,
                        license_number = @License, specialization = @Specialization,
                        status = @Status, certification = @Certification
                    WHERE staff_id = @Id", connection);
                AddParameter(command, "@FirstName",   staff.FirstName);
                AddParameter(command, "@LastName",    staff.LastName);
                AddParameter(command, "@ContactInfo", staff.ContactInfo);
                AddParameter(command, "@IsAvailable", staff.Available);
                AddParameter(command, "@Id",          staff.StaffID);

                if (staff is Doctor doctor)
                {
                    AddParameter(command, "@License",        doctor.LicenseNumber);
                    AddParameter(command, "@Specialization", doctor.Specialization);
                    AddParameter(command, "@Status",         doctor.DoctorStatus.ToString());
                    AddParameter(command, "@Certification",  DBNull.Value);
                }
                else if (staff is Pharmacyst pharmacist)
                {
                    AddParameter(command, "@License",        DBNull.Value);
                    AddParameter(command, "@Specialization", DBNull.Value);
                    AddParameter(command, "@Status",         DBNull.Value);
                    AddParameter(command, "@Certification",  pharmacist.Certification);
                }
                else
                {
                    AddParameter(command, "@License",        DBNull.Value);
                    AddParameter(command, "@Specialization", DBNull.Value);
                    AddParameter(command, "@Status",         DBNull.Value);
                    AddParameter(command, "@Certification",  DBNull.Value);
                }

                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error UpdateStaff: {exception.Message}");
            }
        }

        private static string NormalizeForComparison(string? value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
