using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ShiftRepository : IShiftRepository, IShiftManagementShiftRepository, IPharmacyShiftRepository
    {
        private List<Shift> cachedShifts;
        private readonly string connectionString;
        private readonly StaffRepository staffRepository;

        public ShiftRepository(string connectionString, StaffRepository staffRepository)
        {
            this.connectionString = connectionString;
            this.staffRepository = staffRepository;
            cachedShifts = FetchAllShiftsFromDatabase();
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        private List<Shift> FetchAllShiftsFromDatabase()
        {
            var shifts = new List<Shift>();
            var allStaff = staffRepository.LoadAllStaff();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand("SELECT shift_id, staff_id, location, start_time, end_time, status FROM Shifts", connection);

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
                    var appointedStaff = allStaff.FirstOrDefault(staffMember => staffMember.StaffID == staffId);

                    if (appointedStaff != null)
                    {
                        shifts.Add(new Shift(shiftId, appointedStaff, location, startTime, endTime, shiftStatus));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetShifts: {ex.Message}");
            }
            return shifts;
        }

        public void AddShift(Shift newShift)
        {
            cachedShifts.Add(newShift);
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
                    VALUES (@StaffId, @Location, @StartTime, @EndTime, @Status, @IsActive)", connection);

                AddParameter(command, "@StaffId", newShift.AppointedStaff.StaffID);
                AddParameter(command, "@Location", newShift.Location);
                AddParameter(command, "@StartTime", newShift.StartTime);
                AddParameter(command, "@EndTime", newShift.EndTime);
                AddParameter(command, "@Status", newShift.Status.ToString());
                AddParameter(command, "@IsActive", true);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating new shift: {ex.Message}");
            }
        }

        public void CancelShift(int shiftId)
        {
            var shiftToCancel = cachedShifts.FirstOrDefault(shift => shift.Id == shiftId);
            if (shiftToCancel != null)
            {
                cachedShifts.Remove(shiftToCancel);
                try
                {
                    using var connection = GetConnection();
                    connection.Open();
                    using var command = new SqlCommand("DELETE FROM Shifts WHERE shift_id = @Id", connection);
                    AddParameter(command, "@Id", shiftId);
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting shift: {ex.Message}");
                }
            }
        }

        public List<Shift> GetShifts() => cachedShifts;

        public Shift? GetShiftById(int shiftId)
            => cachedShifts.FirstOrDefault(shift => shift.Id == shiftId);

        public List<Shift> GetShiftsByStaffID(int staffId)
            => cachedShifts.Where(shift => shift.AppointedStaff.StaffID == staffId).ToList();

        public List<Shift> GetActiveShifts()
            => cachedShifts.Where(shift => shift.Status == ShiftStatus.ACTIVE).ToList();

        public float GetWeeklyHours(int staffId)
        {
            var staffShifts = GetShiftsByStaffID(staffId);
            float totalHours = 0;

            const int daysInWeek = 7;
            var now = DateTime.Now;
            int daysFromMonday = (daysInWeek + (now.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            var weekStartMonday = now.Date.AddDays(-daysFromMonday);
            var weekEndSunday = weekStartMonday.AddDays(daysInWeek);

            foreach (var shift in staffShifts)
            {
                if (shift.StartTime >= weekStartMonday && shift.StartTime < weekEndSunday)
                {
                    totalHours += (float)(shift.EndTime - shift.StartTime).TotalHours;
                }
            }

            return totalHours;
        }

        public IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime rangeStart, DateTime rangeEnd)
        {
            return FetchAllShiftsFromDatabase()
                .Where(shift =>
                    shift.AppointedStaff.StaffID == staffId &&
                    shift.StartTime < rangeEnd &&
                    shift.EndTime > rangeStart)
                .OrderBy(shift => shift.StartTime)
                .ToList();
        }

        public bool IsStaffWorkingDuring(int staffId, DateTime startTime, DateTime endTime)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Shifts
                    WHERE staff_id = @StaffId
                      AND start_time < @EndTime
                      AND end_time > @StartTime
                      AND status IN ('SCHEDULED', 'ACTIVE')", connection);
                AddParameter(command, "@StaffId", staffId);
                AddParameter(command, "@StartTime", startTime);
                AddParameter(command, "@EndTime", endTime);

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error IsStaffWorkingDuring: {ex.Message}");
                return false;
            }
        }

        public void UpdateShiftStatus(int shiftId, ShiftStatus status)
        {
            var shiftToUpdate = cachedShifts.FirstOrDefault(shift => shift.Id == shiftId);
            if (shiftToUpdate != null)
            {
                shiftToUpdate.Status = status;
                try
                {
                    using var connection = GetConnection();
                    connection.Open();
                    using var command = new SqlCommand(@"
                        UPDATE Shifts SET
                            staff_id = @StaffId,
                            location = @Location,
                            start_time = @StartTime,
                            end_time = @EndTime,
                            status = @Status
                        WHERE shift_id = @Id", connection);
                    AddParameter(command, "@StaffId", shiftToUpdate.AppointedStaff.StaffID);
                    AddParameter(command, "@Location", shiftToUpdate.Location);
                    AddParameter(command, "@StartTime", shiftToUpdate.StartTime);
                    AddParameter(command, "@EndTime", shiftToUpdate.EndTime);
                    AddParameter(command, "@Status", shiftToUpdate.Status.ToString());
                    AddParameter(command, "@Id", shiftToUpdate.Id);
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating shift: {ex.Message}");
                }
            }
        }

        public void Refresh()
        {
            cachedShifts = FetchAllShiftsFromDatabase();
        }
    }
}
