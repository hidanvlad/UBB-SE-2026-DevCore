using System;
using System.Collections.Generic;
using System.Linq;
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

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public void AddHangout(Hangout hangout)
        {
            int newId = InsertHangout(hangout.Title, hangout.Description, hangout.Date, hangout.MaxParticipants);
            foreach (var participant in hangout.ParticipantList)
            {
                InsertHangoutParticipant(newId, participant.StaffID);
            }
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            InsertHangoutParticipant(hangoutId, staffId);
        }

        public List<Hangout> GetAllHangouts()
        {
            var hangouts = FetchAllHangouts();
            foreach (var hangout in hangouts)
            {
                var participants = FetchHangoutParticipants(hangout.HangoutID);
                hangout.ParticipantList.AddRange(participants);
            }
            return hangouts;
        }

        public Hangout? GetHangoutById(int id)
        {
            var hangout = FetchHangoutById(id);
            if (hangout != null)
            {
                var participants = FetchHangoutParticipants(hangout.HangoutID);
                hangout.ParticipantList.AddRange(participants);
            }
            return hangout;
        }

        public bool HasConflictsOnDate(int staffId, DateTime date)
        {
            var statuses = GetAppointmentStatusesForStaffOnDate(staffId, date);
            var activeConflicts = statuses.Where(appointmentStatus =>
                !string.Equals(appointmentStatus, "Finished", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(appointmentStatus, "Canceled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(appointmentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase));
            return activeConflicts.Any();
        }

        private int InsertHangout(string title, string description, DateTime date, int maxParticipants)
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqlCommand(@"
                INSERT INTO Hangouts (title, description, date_time, max_staff)
                OUTPUT INSERTED.hangout_id
                VALUES (@Title, @Description, @Date, @MaxStaff);", connection);

            AddParameter(command, "@Title", title);
            AddParameter(command, "@Description", string.IsNullOrEmpty(description) ? DBNull.Value : (object)description);
            AddParameter(command, "@Date", date);
            AddParameter(command, "@MaxStaff", maxParticipants);

            return (int)command.ExecuteScalar();
        }

        private void InsertHangoutParticipant(int hangoutId, int staffId)
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqlCommand("INSERT INTO Hangout_Participants (hangout_id, staff_id) VALUES (@HId, @SId)", connection);
            AddParameter(command, "@HId", hangoutId);
            AddParameter(command, "@SId", staffId);
            command.ExecuteNonQuery();
        }

        private List<Hangout> FetchAllHangouts()
        {
            var hangouts = new List<Hangout>();
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqlCommand("SELECT hangout_id, title, description, date_time, max_staff FROM Hangouts", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                hangouts.Add(new Hangout(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetInt32(4)));
            }
            return hangouts;
        }

        private Hangout? FetchHangoutById(int id)
        {
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqlCommand("SELECT hangout_id, title, description, date_time, max_staff FROM Hangouts WHERE hangout_id = @Id", connection);
            AddParameter(command, "@Id", id);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Hangout(
                    id,
                    reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetInt32(4));
            }
            return null;
        }

        private List<IStaff> FetchHangoutParticipants(int hangoutId)
        {
            var participants = new List<IStaff>();
            using var connection = GetConnection();
            connection.Open();
            using var command = new SqlCommand(@"
                SELECT s.staff_id, s.first_name, s.last_name
                FROM Hangout_Participants hp
                JOIN Staff s ON hp.staff_id = s.staff_id
                WHERE hp.hangout_id = @HId", connection);
            AddParameter(command, "@HId", hangoutId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                participants.Add(new Doctor
                {
                    StaffID = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2)
                });
            }
            return participants;
        }

        private List<string> GetAppointmentStatusesForStaffOnDate(int staffId, DateTime date)
        {
            var statuses = new List<string>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = new SqlCommand(@"
                    SELECT status
                    FROM Appointments
                    WHERE doctor_id = @StaffId
                      AND CAST(start_time AS DATE) = CAST(@Date AS DATE)", connection);

                AddParameter(command, "@StaffId", staffId);
                AddParameter(command, "@Date", date.Date);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    statuses.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error GetAppointmentStatuses: {ex.Message}");
            }
            return statuses;
        }
    }
}
