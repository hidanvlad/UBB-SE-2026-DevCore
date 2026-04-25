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

        public int AddShiftSwapRequest(ShiftSwapRequest request)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
                OUTPUT INSERTED.swap_id
                VALUES (@ShiftId, @RequesterId, @ColleagueId, @RequestedAt, @Status);", connection);

            AddParameter(command, "@ShiftId", request.ShiftId);
            AddParameter(command, "@RequesterId", request.RequesterId);
            AddParameter(command, "@ColleagueId", request.ColleagueId);
            AddParameter(command, "@RequestedAt", request.RequestedAt);
            AddParameter(command, "@Status", request.Status.ToString());

            return (int)command.ExecuteScalar();
        }

        public IReadOnlyList<ShiftSwapRequest> GetAllShiftSwapRequests()
        {
            var swapRequests = new List<ShiftSwapRequest>();
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status FROM ShiftSwapRequests;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int swapIdOrdinal = reader.GetOrdinal("swap_id");
            int shiftIdOrdinal = reader.GetOrdinal("shift_id");
            int requesterIdOrdinal = reader.GetOrdinal("requester_id");
            int colleagueIdOrdinal = reader.GetOrdinal("colleague_id");
            int requestedAtOrdinal = reader.GetOrdinal("requested_at");
            int statusOrdinal = reader.GetOrdinal("status");
            while (reader.Read())
            {
                Enum.TryParse<ShiftSwapRequestStatus>(reader.GetString(statusOrdinal), true, out var swapRequestStatus);
                swapRequests.Add(new ShiftSwapRequest
                {
                    SwapId = reader.GetInt32(swapIdOrdinal),
                    ShiftId = reader.GetInt32(shiftIdOrdinal),
                    RequesterId = reader.GetInt32(requesterIdOrdinal),
                    ColleagueId = reader.GetInt32(colleagueIdOrdinal),
                    RequestedAt = reader.GetDateTime(requestedAtOrdinal),
                    Status = swapRequestStatus,
                });
            }
            return swapRequests;
        }

        public ShiftSwapRequest? GetShiftSwapRequestById(int swapId)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT swap_id, shift_id, requester_id, colleague_id, requested_at, status FROM ShiftSwapRequests WHERE swap_id = @SwapId;",
                connection);
            AddParameter(command, "@SwapId", swapId);

            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            int swapIdOrdinal = reader.GetOrdinal("swap_id");
            int shiftIdOrdinal = reader.GetOrdinal("shift_id");
            int requesterIdOrdinal = reader.GetOrdinal("requester_id");
            int colleagueIdOrdinal = reader.GetOrdinal("colleague_id");
            int requestedAtOrdinal = reader.GetOrdinal("requested_at");
            int statusOrdinal = reader.GetOrdinal("status");
            Enum.TryParse<ShiftSwapRequestStatus>(reader.GetString(statusOrdinal), true, out var swapRequestStatus);
            return new ShiftSwapRequest
            {
                SwapId = reader.GetInt32(swapIdOrdinal),
                ShiftId = reader.GetInt32(shiftIdOrdinal),
                RequesterId = reader.GetInt32(requesterIdOrdinal),
                ColleagueId = reader.GetInt32(colleagueIdOrdinal),
                RequestedAt = reader.GetDateTime(requestedAtOrdinal),
                Status = swapRequestStatus,
            };
        }

        public void UpdateShiftSwapRequestStatus(int swapId, string status)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "UPDATE ShiftSwapRequests SET status = @Status WHERE swap_id = @SwapId;",
                connection);
            AddParameter(command, "@Status", status);
            AddParameter(command, "@SwapId", swapId);
            command.ExecuteNonQuery();
        }

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
