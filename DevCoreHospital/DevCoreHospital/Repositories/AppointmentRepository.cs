using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class AppointmentRepository : IDoctorAppointmentDataSource
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

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            var appointments = new List<Appointment>();
            var toDate = fromDate.Date.AddDays(31);

            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(@"
                SELECT a.appointment_id, a.doctor_id,
                       LTRIM(RTRIM(CONCAT(COALESCE(s.first_name, ''), ' ', COALESCE(s.last_name, '')))) AS DoctorName,
                       a.patient_id, a.start_time, a.end_time, a.status
                FROM Appointments a
                INNER JOIN Staff s ON s.staff_id = a.doctor_id
                WHERE a.doctor_id = @DocId AND a.start_time >= @From AND a.start_time < @To
                ORDER BY a.start_time
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;", connection);

            AddParameter(command, "@DocId", doctorUserId);
            AddParameter(command, "@From", fromDate.Date);
            AddParameter(command, "@To", toDate);
            AddParameter(command, "@Skip", skip);
            AddParameter(command, "@Take", take);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                appointments.Add(MapReaderToAppointment(reader, hasDoctorName: true));
            }
            return appointments;
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var doctors = new List<(int, string)>();
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand(@"
                SELECT staff_id,
                       LTRIM(RTRIM(CONCAT(COALESCE(first_name, ''), ' ', COALESCE(last_name, ''))))
                FROM Staff
                WHERE role = 'Doctor'
                ORDER BY first_name;", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                doctors.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }
            return doctors;
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT appointment_id, doctor_id, patient_id, start_time, end_time, status FROM Appointments WHERE appointment_id = @Id;", connection);
            AddParameter(command, "@Id", appointmentId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToAppointment(reader, hasDoctorName: false);
            }
            return null;
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var appointments = new List<Appointment>();
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT appointment_id, doctor_id, patient_id, start_time, end_time, status FROM Appointments WHERE doctor_id = @DocId ORDER BY start_time;", connection);
            AddParameter(command, "@DocId", doctorId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                appointments.Add(MapReaderToAppointment(reader, hasDoctorName: false));
            }
            return appointments;
        }

        public async Task AddAppointmentAsync(Appointment appt)
        {
            string rawPatientInput = appt.PatientName?.Replace("PAT-", string.Empty).Trim() ?? "0";
            int.TryParse(rawPatientInput, out int patientId);

            DateTime startTimeDb = appt.Date.Date.Add(appt.StartTime);
            DateTime endTimeDb = appt.Date.Date.Add(appt.EndTime);

            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status) VALUES (@PatId, @DocId, @Start, @End, 'Scheduled');", connection);
            AddParameter(command, "@PatId", patientId);
            AddParameter(command, "@DocId", appt.DoctorId);
            AddParameter(command, "@Start", startTimeDb);
            AddParameter(command, "@End", endTimeDb);
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAppointmentStatusAsync(int id, string status)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("UPDATE Appointments SET status = @Status WHERE appointment_id = @Id;", connection);
            AddParameter(command, "@Status", status);
            AddParameter(command, "@Id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT COUNT(*) FROM Appointments WHERE doctor_id = @DocId AND status = 'Scheduled';", connection);
            AddParameter(command, "@DocId", doctorId);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new SqlCommand("UPDATE Staff SET status = @Status WHERE staff_id = @DocId AND role = 'Doctor';", connection);
            AddParameter(command, "@Status", status);
            AddParameter(command, "@DocId", doctorId);
            await command.ExecuteNonQueryAsync();
        }

        private static Appointment MapReaderToAppointment(SqlDataReader reader, bool hasDoctorName)
        {
            int startOrdinal = reader.GetOrdinal("start_time");
            int endOrdinal = reader.GetOrdinal("end_time");
            int patientOrdinal = reader.GetOrdinal("patient_id");
            int statusOrdinal = reader.GetOrdinal("status");
            int doctorNameOrdinal = hasDoctorName ? reader.GetOrdinal("DoctorName") : -1;

            DateTime startDateTime = reader.IsDBNull(startOrdinal) ? DateTime.Now : reader.GetDateTime(startOrdinal);
            DateTime endDateTime = reader.IsDBNull(endOrdinal) ? startDateTime : reader.GetDateTime(endOrdinal);
            int patientId = reader.IsDBNull(patientOrdinal) ? 0 : reader.GetInt32(patientOrdinal);

            return new Appointment
            {
                Id = reader.GetInt32(reader.GetOrdinal("appointment_id")),
                DoctorId = reader.GetInt32(reader.GetOrdinal("doctor_id")),
                DoctorName = hasDoctorName && !reader.IsDBNull(doctorNameOrdinal) ? reader.GetString(doctorNameOrdinal) : string.Empty,
                PatientName = "PAT-" + patientId,
                Date = startDateTime.Date,
                StartTime = startDateTime.TimeOfDay,
                EndTime = endDateTime.TimeOfDay,
                Status = reader.IsDBNull(statusOrdinal) ? "Scheduled" : reader.GetString(statusOrdinal),
                Type = string.Empty,
                Location = string.Empty
            };
        }
    }
}
