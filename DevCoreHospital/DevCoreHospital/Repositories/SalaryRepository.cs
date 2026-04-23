using System;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class SalaryRepository
    {
        private const int FallbackMedicinesSoldCount = 150;

        private readonly string connectionString;

        public SalaryRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public virtual double GetShiftHoursFromDb(int shiftId)
        {
            double totalHours = 0;
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand("SELECT start_time, end_time FROM Shifts WHERE shift_id = @ShiftId", connection);
                AddParameter(command, "@ShiftId", shiftId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    DateTime startTime = reader.GetDateTime(0);
                    DateTime endTime = reader.GetDateTime(1);
                    totalHours = (endTime - startTime).TotalHours;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetShiftHoursFromDb: {ex.Message}");
            }
            return totalHours;
        }

        public virtual int GetMedicinesSold(int pharmacistStaffId, int month, int year)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT COUNT(*) FROM PharmacyHandover
                    WHERE PharmacistID = @staffId AND MONTH(HandoverDate) = @month AND YEAR(HandoverDate) = @year", connection);

                AddParameter(command, "@staffId", pharmacistStaffId);
                AddParameter(command, "@month", month);
                AddParameter(command, "@year", year);

                var result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch
            {
                return FallbackMedicinesSoldCount;
            }
        }

        public virtual bool DidStaffParticipateInHangout(int staffId, int month, int year)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Hangout_Participants hp
                    JOIN Hangouts h ON hp.hangout_id = h.hangout_id
                    WHERE hp.staff_id = @StaffId
                      AND MONTH(h.date_time) = @Month
                      AND YEAR(h.date_time) = @Year", connection);

                AddParameter(command, "@StaffId", staffId);
                AddParameter(command, "@Month", month);
                AddParameter(command, "@Year", year);

                int hangoutParticipationCount = Convert.ToInt32(command.ExecuteScalar());
                return hangoutParticipationCount > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking hangout bonus: {ex.Message}");
                return false;
            }
        }
    }
}
