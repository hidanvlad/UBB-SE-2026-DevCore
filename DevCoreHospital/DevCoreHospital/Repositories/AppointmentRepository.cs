using DevCoreHospital.Data;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Repositories
{
    public sealed class AppointmentRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public AppointmentRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorId, DateTime fromDate, int skip, int take)
        {
            var result = new List<Appointment>();

            const string sql = @"
SELECT appointment_id, patient_id, doctor_id, date_time, status
FROM dbo.Appointments
WHERE doctor_id = @DoctorId
  AND date_time >= @FromDate
ORDER BY date_time
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DoctorId", doctorId);
            cmd.Parameters.AddWithValue("@FromDate", fromDate);
            cmd.Parameters.AddWithValue("@Skip", Math.Max(0, skip));
            cmd.Parameters.AddWithValue("@Take", Math.Max(1, take));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new Appointment
                {
                    Id = reader.GetInt32(reader.GetOrdinal("appointment_id")),
                    PatientId = reader.GetInt32(reader.GetOrdinal("patient_id")),
                    DoctorId = reader.GetInt32(reader.GetOrdinal("doctor_id")),
                    DateTime = reader.GetDateTime(reader.GetOrdinal("date_time")),
                    Status = reader["status"] as string ?? string.Empty
                });
            }

            return result;
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var result = new List<(int DoctorId, string DoctorName)>();

            const string sql = @"
SELECT staff_id, first_name, last_name
FROM dbo.Staff
WHERE UPPER(role) = 'DOCTOR'
ORDER BY first_name, last_name;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("staff_id"));
                var first = reader["first_name"] as string ?? string.Empty;
                var last = reader["last_name"] as string ?? string.Empty;
                var fullName = $"{first} {last}".Trim();

                result.Add((id, fullName));
            }

            return result;
        }

        public async Task<AppointmentDetails> GetAppointmentDetailsAsync(int appointmentId)
        {
            const string sql = @"
SELECT appointment_id, patient_id, doctor_id, date_time, status
FROM dbo.Appointments
WHERE appointment_id = @AppointmentId;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AppointmentId", appointmentId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new AppointmentDetails
            {
                Id = reader.GetInt32(reader.GetOrdinal("appointment_id")),
                PatientId = reader.GetInt32(reader.GetOrdinal("patient_id")),
                DoctorId = reader.GetInt32(reader.GetOrdinal("doctor_id")),
                DateTime = reader.GetDateTime(reader.GetOrdinal("date_time")),
                Status = reader["status"] as string ?? string.Empty
            };
        }

        public async Task<int> AddAppointmentAsync(Appointment appointment)
        {
            const string sql = @"
INSERT INTO dbo.Appointments(patient_id, doctor_id, date_time, status)
VALUES (@PatientId, @DoctorId, @DateTime, @Status);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PatientId", appointment.PatientId);
            cmd.Parameters.AddWithValue("@DoctorId", appointment.DoctorId);
            cmd.Parameters.AddWithValue("@DateTime", appointment.DateTime);
            cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(appointment.Status) ? "Scheduled" : appointment.Status);

            var insertedIdObj = await cmd.ExecuteScalarAsync();
            var insertedId = insertedIdObj is int x ? x : Convert.ToInt32(insertedIdObj);

            appointment.Id = insertedId;
            return insertedId;
        }

        public async Task UpdateAppointmentStatusAsync(int appointmentId, string status)
        {
            const string sql = @"
UPDATE dbo.Appointments
SET status = @Status
WHERE appointment_id = @AppointmentId;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", status ?? string.Empty);
            cmd.Parameters.AddWithValue("@AppointmentId", appointmentId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.Appointments
WHERE doctor_id = @DoctorId
  AND status IN ('Scheduled', 'InProgress');";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DoctorId", doctorId);

            var countObj = await cmd.ExecuteScalarAsync();
            return countObj is int i ? i : Convert.ToInt32(countObj);
        }

        public async Task UpdateDoctorAvailabilityAsync(int doctorId, bool isAvailable)
        {
            const string sql = @"
UPDATE dbo.Staff
SET is_available = @IsAvailable
WHERE staff_id = @DoctorId;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IsAvailable", isAvailable);
            cmd.Parameters.AddWithValue("@DoctorId", doctorId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            const string sql = @"
UPDATE dbo.Staff
SET status = @Status
WHERE staff_id = @DoctorId;";

            using var conn = _connectionFactory.Create();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", status ?? string.Empty);
            cmd.Parameters.AddWithValue("@DoctorId", doctorId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}