using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class HangoutParticipantRepository : IHangoutParticipantRepository
    {
        private readonly string connectionString;

        public HangoutParticipantRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IReadOnlyList<(int HangoutId, int StaffId)> GetAllParticipants()
        {
            var participants = new List<(int HangoutId, int StaffId)>();
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT hangout_id, staff_id FROM Hangout_Participants;",
                connection);
            using SqlDataReader reader = command.ExecuteReader();
            int hangoutOrdinal = reader.GetOrdinal("hangout_id");
            int staffOrdinal = reader.GetOrdinal("staff_id");
            while (reader.Read())
            {
                participants.Add((reader.GetInt32(hangoutOrdinal), reader.GetInt32(staffOrdinal)));
            }
            return participants;
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "INSERT INTO Hangout_Participants (hangout_id, staff_id) VALUES (@HangoutId, @StaffId);",
                connection);
            command.Parameters.Add(new SqlParameter("@HangoutId", hangoutId));
            command.Parameters.Add(new SqlParameter("@StaffId", staffId));
            command.ExecuteNonQuery();
        }
    }
}
