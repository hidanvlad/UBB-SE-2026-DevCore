using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ShiftSwapRepository : IShiftSwapRepository
    {
        private readonly string connectionString;

        public ShiftSwapRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public int CreateShiftSwapRequest(ShiftSwapRequest request)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
                    VALUES (@ShiftId, @RequesterId, @ColleagueId, @RequestedAt, @Status);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", connection);

                AddParameter(command, "@ShiftId", request.ShiftId);
                AddParameter(command, "@RequesterId", request.RequesterId);
                AddParameter(command, "@ColleagueId", request.ColleagueId);
                AddParameter(command, "@RequestedAt", request.RequestedAt);
                AddParameter(command, "@Status", request.Status.ToString());

                var result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error CreateShiftSwapRequest: {ex.Message}");
                return 0;
            }
        }

        public List<ShiftSwapRequest> GetPendingSwapRequestsForColleague(int colleagueId)
        {
            var swapRequests = new List<ShiftSwapRequest>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status
                    FROM ShiftSwapRequests
                    WHERE colleague_id = @ColleagueId AND status = 'PENDING'
                    ORDER BY requested_at DESC", connection);
                AddParameter(command, "@ColleagueId", colleagueId);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Enum.TryParse<ShiftSwapRequestStatus>(reader.GetString(5), true, out var shiftSwapRequestStatus);
                    swapRequests.Add(new ShiftSwapRequest
                    {
                        SwapId = reader.GetInt32(0),
                        ShiftId = reader.GetInt32(1),
                        RequesterId = reader.GetInt32(2),
                        ColleagueId = reader.GetInt32(3),
                        RequestedAt = reader.GetDateTime(4),
                        Status = shiftSwapRequestStatus
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetPendingSwapRequestsForColleague: {ex.Message}");
            }
            return swapRequests;
        }

        public ShiftSwapRequest? GetShiftSwapRequestById(int swapId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status
                    FROM ShiftSwapRequests
                    WHERE swap_id = @SwapId", connection);
                AddParameter(command, "@SwapId", swapId);

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                Enum.TryParse<ShiftSwapRequestStatus>(reader.GetString(5), true, out var shiftSwapRequestStatus);
                return new ShiftSwapRequest
                {
                    SwapId = reader.GetInt32(0),
                    ShiftId = reader.GetInt32(1),
                    RequesterId = reader.GetInt32(2),
                    ColleagueId = reader.GetInt32(3),
                    RequestedAt = reader.GetDateTime(4),
                    Status = shiftSwapRequestStatus
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetShiftSwapRequestById: {ex.Message}");
                return null;
            }
        }

        public bool UpdateShiftSwapRequestStatus(int swapId, string status)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand("UPDATE ShiftSwapRequests SET status = @Status WHERE swap_id = @SwapId;", connection);
                AddParameter(command, "@Status", status);
                AddParameter(command, "@SwapId", swapId);
                return command.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error UpdateShiftSwapRequestStatus: {ex.Message}");
                return false;
            }
        }

        public void AddNotification(int recipientStaffId, string title, string message)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    INSERT INTO Notifications (recipient_staff_id, title, message, created_at, is_read)
                    VALUES (@RecipientId, @Title, @Message, @CreatedAt, 0)", connection);
                AddParameter(command, "@RecipientId", recipientStaffId);
                AddParameter(command, "@Title", title);
                AddParameter(command, "@Message", message);
                AddParameter(command, "@CreatedAt", DateTime.UtcNow);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification fallback -> To:{recipientStaffId} | {title} | {message}");
                System.Diagnostics.Debug.WriteLine($"Error AddNotification: {ex.Message}");
            }
        }

        public bool ReassignShiftToStaff(int shiftId, int newStaffId)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand("UPDATE Shifts SET staff_id = @StaffId WHERE shift_id = @ShiftId;", connection);
                AddParameter(command, "@StaffId", newStaffId);
                AddParameter(command, "@ShiftId", shiftId);
                return command.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ReassignShiftToStaff: {ex.Message}");
                return false;
            }
        }
    }
}
