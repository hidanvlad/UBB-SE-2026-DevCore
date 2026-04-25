using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly string connectionString;

        public AppointmentRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(connectionString);

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public async Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync()
        {
            var appointments = new List<Appointment>();

            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(
                "SELECT appointment_id, doctor_id, patient_id, start_time, end_time, status FROM Appointments;",
                connection);

            using var reader = await command.ExecuteReaderAsync();
            int idOrdinal = reader.GetOrdinal("appointment_id");
            int doctorIdOrdinal = reader.GetOrdinal("doctor_id");
            int patientIdOrdinal = reader.GetOrdinal("patient_id");
            int startOrdinal = reader.GetOrdinal("start_time");
            int endOrdinal = reader.GetOrdinal("end_time");
            int statusOrdinal = reader.GetOrdinal("status");

            while (await reader.ReadAsync())
            {
                DateTime startDateTime = reader.GetDateTime(startOrdinal);
                DateTime endDateTime = reader.GetDateTime(endOrdinal);
                appointments.Add(new Appointment
                {
                    Id = reader.GetInt32(idOrdinal),
                    DoctorId = reader.GetInt32(doctorIdOrdinal),
                    DoctorName = string.Empty,
                    PatientName = reader.GetInt32(patientIdOrdinal).ToString(),
                    Date = startDateTime.Date,
                    StartTime = startDateTime.TimeOfDay,
                    EndTime = endDateTime.TimeOfDay,
                    Status = reader.GetString(statusOrdinal),
                });
            }
            return appointments;
        }

        public async Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime, string status)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(
                "INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status) VALUES (@PatientId, @DoctorId, @StartTime, @EndTime, @Status);",
                connection);
            AddParameter(command, "@PatientId", patientId);
            AddParameter(command, "@DoctorId", doctorId);
            AddParameter(command, "@StartTime", startTime);
            AddParameter(command, "@EndTime", endTime);
            AddParameter(command, "@Status", status);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAppointmentStatusAsync(int id, string status)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(
                "UPDATE Appointments SET status = @Status WHERE appointment_id = @Id;",
                connection);
            AddParameter(command, "@Status", status);
            AddParameter(command, "@Id", id);
            await command.ExecuteNonQueryAsync();
        }
    }
}
