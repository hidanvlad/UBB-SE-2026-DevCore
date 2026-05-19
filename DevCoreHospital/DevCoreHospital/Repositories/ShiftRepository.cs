using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ShiftRepository : IShiftRepository, IShiftManagementShiftRepository, IPharmacyShiftRepository
    {
        private const string DefaultShiftStatusLabel = "Scheduled";

        private readonly string connectionString;

        public ShiftRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IReadOnlyList<Shift> GetAllShifts()
        {
            var shifts = new List<Shift>();

            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT shift_id, staff_id, location, start_time, end_time, status FROM Shifts;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int shiftIdOrdinal = reader.GetOrdinal("shift_id");
            int staffIdOrdinal = reader.GetOrdinal("staff_id");
            int locationOrdinal = reader.GetOrdinal("location");
            int startOrdinal = reader.GetOrdinal("start_time");
            int endOrdinal = reader.GetOrdinal("end_time");
            int statusOrdinal = reader.GetOrdinal("status");

            while (reader.Read())
            {
                int staffId = reader.GetInt32(staffIdOrdinal);
                string location = reader.IsDBNull(locationOrdinal) ? string.Empty : reader.GetString(locationOrdinal);
                DateTime startTime = reader.GetDateTime(startOrdinal);
                DateTime endTime = reader.GetDateTime(endOrdinal);
                string statusLabel = reader.IsDBNull(statusOrdinal) ? DefaultShiftStatusLabel : reader.GetString(statusOrdinal);
                Enum.TryParse<ShiftStatus>(statusLabel, true, out ShiftStatus shiftStatus);

                IStaff appointedStaff = new Doctor { StaffID = staffId };
                shifts.Add(new Shift(reader.GetInt32(shiftIdOrdinal), appointedStaff, location, startTime, endTime, shiftStatus));
            }
            return shifts;
        }

        public void AddShift(Shift newShift)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
                VALUES (@StaffId, @Location, @StartTime, @EndTime, @Status, 1);", connection);
            AddParameter(command, "@StaffId", newShift.AppointedStaff.StaffID);
            AddParameter(command, "@Location", newShift.Location);
            AddParameter(command, "@StartTime", newShift.StartTime);
            AddParameter(command, "@EndTime", newShift.EndTime);
            AddParameter(command, "@Status", newShift.Status.ToString());
            command.ExecuteNonQuery();
        }

        public void UpdateShiftStatus(int shiftId, ShiftStatus status)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "UPDATE Shifts SET status = @Status WHERE shift_id = @ShiftId;", connection);
            AddParameter(command, "@Status", status.ToString());
            AddParameter(command, "@ShiftId", shiftId);
            command.ExecuteNonQuery();
        }

        public void UpdateShiftStaffId(int shiftId, int newStaffId)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "UPDATE Shifts SET staff_id = @NewStaffId WHERE shift_id = @ShiftId;", connection);
            AddParameter(command, "@NewStaffId", newStaffId);
            AddParameter(command, "@ShiftId", shiftId);
            command.ExecuteNonQuery();
        }

        public void DeleteShift(int shiftId)
        {
            using SqlConnection connection = GetConnection();
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "DELETE FROM Shifts WHERE shift_id = @ShiftId;", connection);
            AddParameter(command, "@ShiftId", shiftId);
            command.ExecuteNonQuery();
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
