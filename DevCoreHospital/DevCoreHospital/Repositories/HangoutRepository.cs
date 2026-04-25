using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class HangoutRepository : IHangoutRepository
    {
        private readonly string connectionString;

        public HangoutRepository()
        {
            connectionString = AppSettings.ConnectionString;
        }

        public HangoutRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public int AddHangout(string title, string description, DateTime date, int maxParticipants)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO Hangouts (title, description, date_time, max_staff)
                OUTPUT INSERTED.hangout_id
                VALUES (@Title, @Description, @Date, @MaxStaff);", connection);

            AddParameter(command, "@Title", title);
            AddParameter(command, "@Description", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
            AddParameter(command, "@Date", date);
            AddParameter(command, "@MaxStaff", maxParticipants);

            return (int)command.ExecuteScalar();
        }

        public List<Hangout> GetAllHangouts()
        {
            var hangouts = new List<Hangout>();
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT hangout_id, title, description, date_time, max_staff FROM Hangouts;",
                connection);
            using SqlDataReader reader = command.ExecuteReader();
            int idOrdinal = reader.GetOrdinal("hangout_id");
            int titleOrdinal = reader.GetOrdinal("title");
            int descriptionOrdinal = reader.GetOrdinal("description");
            int dateOrdinal = reader.GetOrdinal("date_time");
            int maxStaffOrdinal = reader.GetOrdinal("max_staff");
            while (reader.Read())
            {
                hangouts.Add(new Hangout(
                    reader.GetInt32(idOrdinal),
                    reader.GetString(titleOrdinal),
                    reader.IsDBNull(descriptionOrdinal) ? string.Empty : reader.GetString(descriptionOrdinal),
                    reader.GetDateTime(dateOrdinal),
                    reader.GetInt32(maxStaffOrdinal)));
            }
            return hangouts;
        }

        public Hangout? GetHangoutById(int hangoutId)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT hangout_id, title, description, date_time, max_staff FROM Hangouts WHERE hangout_id = @HangoutId;",
                connection);
            AddParameter(command, "@HangoutId", hangoutId);
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            int idOrdinal = reader.GetOrdinal("hangout_id");
            int titleOrdinal = reader.GetOrdinal("title");
            int descriptionOrdinal = reader.GetOrdinal("description");
            int dateOrdinal = reader.GetOrdinal("date_time");
            int maxStaffOrdinal = reader.GetOrdinal("max_staff");
            return new Hangout(
                reader.GetInt32(idOrdinal),
                reader.GetString(titleOrdinal),
                reader.IsDBNull(descriptionOrdinal) ? string.Empty : reader.GetString(descriptionOrdinal),
                reader.GetDateTime(dateOrdinal),
                reader.GetInt32(maxStaffOrdinal));
        }

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
