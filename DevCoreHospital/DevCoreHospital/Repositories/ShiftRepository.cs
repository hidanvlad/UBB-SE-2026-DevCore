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
        private readonly StaffRepository staffRepository;

        public ShiftRepository(string connectionString, StaffRepository staffRepository)
        {
            this.connectionString = connectionString;
            this.staffRepository = staffRepository;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        // Fetches all shifts from DB, joining with staff from StaffRepository.
        private List<Shift> FetchAllShiftsFromDatabase()
        {
            var shifts = new List<Shift>();
            var allStaff = staffRepository.LoadAllStaff();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(
                    "SELECT shift_id, staff_id, location, start_time, end_time, status FROM Shifts",
                    connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int shiftId = reader.GetInt32(0);
                    int staffId = reader.GetInt32(1);
                    string location = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    DateTime startTime = reader.GetDateTime(3);
                    DateTime endTime = reader.GetDateTime(4);
                    string statusText = reader.IsDBNull(5) ? "Scheduled" : reader.GetString(5);

                    Enum.TryParse<ShiftStatus>(statusText, true, out ShiftStatus shiftStatus);
                    bool HasMatchingStaffId(IStaff staffMember) => staffMember.StaffID == staffId;
                    var appointedStaff = allStaff.FirstOrDefault(HasMatchingStaffId);

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
