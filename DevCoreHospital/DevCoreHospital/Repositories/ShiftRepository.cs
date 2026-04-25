using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ShiftRepository : IShiftRepository, IShiftManagementShiftRepository, IPharmacyShiftRepository
    {
        private readonly string connectionString;

        public ShiftRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        private static IStaff? CreateStaffFromReader(SqlDataReader reader)
        {
            int staffId = reader.GetInt32(5);
            string role = reader.GetString(6);
            string firstName = reader.GetString(7);
            string lastName = reader.GetString(8);
            string contactInfo = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
            bool isAvailable = reader.GetBoolean(10);
            string licenseNumber = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            string specialization = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
            string statusText = reader.IsDBNull(13) ? "Available" : reader.GetString(13);
            string certification = reader.IsDBNull(14) ? string.Empty : reader.GetString(14);
            int yearsOfExperience = reader.IsDBNull(15) ? 0 : reader.GetInt32(15);

            Enum.TryParse<DoctorStatus>(statusText, true, out DoctorStatus doctorStatus);

            if (role == "Doctor")
            {
                return new Doctor(staffId, firstName, lastName, contactInfo, string.Empty, isAvailable,
                    specialization, licenseNumber, doctorStatus, yearsOfExperience);
            }

            if (role == "Pharmacist")
            {
                return new Pharmacyst(staffId, firstName, lastName, contactInfo, isAvailable, certification,
                    yearsOfExperience);
            }

            return null;
        }

        // Fetches all shifts from DB and materializes staff from the joined staff row.
        private List<Shift> FetchAllShiftsFromDatabase()
        {
            var shifts = new List<Shift>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(
                    @"SELECT s.shift_id, s.location, s.start_time, s.end_time, s.status,
                             st.staff_id, st.role, st.first_name, st.last_name, st.contact_info,
                             st.is_available, st.license_number, st.specialization, st.status,
                             st.certification, st.years_of_experience
                      FROM Shifts s
                      INNER JOIN Staff st ON s.staff_id = st.staff_id",
                    connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int shiftId = reader.GetInt32(0);
                    string location = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    DateTime startTime = reader.GetDateTime(2);
                    DateTime endTime = reader.GetDateTime(3);
                    string statusText = reader.IsDBNull(4) ? "Scheduled" : reader.GetString(4);

                    Enum.TryParse<ShiftStatus>(statusText, true, out ShiftStatus shiftStatus);
                    var appointedStaff = CreateStaffFromReader(reader);

                    if (appointedStaff != null)
                    {
                        shifts.Add(new Shift(shiftId, appointedStaff, location, startTime, endTime, shiftStatus));
                    }
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error FetchAllShiftsFromDatabase: {exception.Message}");
            }
            return shifts;
        }

        // ── Read methods ────────────────────────────────────────────────────
        public List<Shift> GetShifts() => FetchAllShiftsFromDatabase();

        public Shift? GetShiftById(int shiftId)
        {
            bool HasMatchingId(Shift shift) => shift.Id == shiftId;
            return FetchAllShiftsFromDatabase().FirstOrDefault(HasMatchingId);
        }

        public List<Shift> GetShiftsByStaffID(int staffId)
        {
            bool HasMatchingStaffId(Shift shift) => shift.AppointedStaff.StaffID == staffId;
            return FetchAllShiftsFromDatabase().Where(HasMatchingStaffId).ToList();
        }

        public IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime rangeStart, DateTime rangeEnd)
        {
            bool IsInRangeForStaff(Shift shift) =>
                shift.AppointedStaff.StaffID == staffId &&
                shift.StartTime < rangeEnd &&
                shift.EndTime > rangeStart;
            DateTime GetStartTime(Shift shift) => shift.StartTime;

            return FetchAllShiftsFromDatabase()
                .Where(IsInRangeForStaff)
                .OrderBy(GetStartTime)
                .ToList();
        }

        // ── Write methods ───────────────────────────────────────────────────
        public void AddShift(Shift newShift)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
                    VALUES (@StaffId, @Location, @StartTime, @EndTime, @Status, 1)", connection);
                AddParameter(command, "@StaffId", newShift.AppointedStaff.StaffID);
                AddParameter(command, "@Location", newShift.Location);
                AddParameter(command, "@StartTime", newShift.StartTime);
                AddParameter(command, "@EndTime", newShift.EndTime);
                AddParameter(command, "@Status", newShift.Status.ToString());
                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error AddShift: {exception.Message}");
            }
        }

        public void UpdateShiftStatus(int shiftId, ShiftStatus status)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(
                    "UPDATE Shifts SET status = @Status WHERE shift_id = @Id", connection);
                AddParameter(command, "@Status", status.ToString());
                AddParameter(command, "@Id", shiftId);
                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error UpdateShiftStatus: {exception.Message}");
            }
        }

        public void CancelShift(int shiftId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(
                    "DELETE FROM Shifts WHERE shift_id = @Id", connection);
                AddParameter(command, "@Id", shiftId);
                command.ExecuteNonQuery();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Error CancelShift: {exception.Message}");
            }
        }
    }
}
