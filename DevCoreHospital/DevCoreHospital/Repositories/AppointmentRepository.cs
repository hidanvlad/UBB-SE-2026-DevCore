using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace DevCoreHospital.Repositories
{
    public class AppointmentRepository : IDoctorAppointmentDataSource
    {
        private readonly DatabaseManager _dbManager;

        public AppointmentRepository(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            var items = new List<Appointment>();
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var doctorsTable = await ResolveDoctorsTableAsync(conn);
            var appointmentsTable = await ResolveAppointmentsTableAsync(conn);

            var to = fromDate.Date.AddDays(8);
            var sql = $@"
SELECT 
    a.Id, a.DoctorId, d.FirstName + ' ' + d.LastName AS DoctorName, a.PatientName,
    CAST(a.[Date] AS datetime2) AS [Date], a.StartTime, a.EndTime, 
    ISNULL(a.Status, '') AS [Status], ISNULL(a.Type, '') AS [Type], ISNULL(a.Location, '') AS [Location]
FROM {appointmentsTable} a
INNER JOIN {doctorsTable} d ON d.StaffID = a.DoctorId
WHERE a.DoctorId = @DoctorId AND CAST(a.[Date] AS date) >= @FromDate AND CAST(a.[Date] AS date) < @ToDate
ORDER BY a.[Date], a.StartTime OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameter(cmd, "@DoctorId", doctorUserId); AddParameter(cmd, "@FromDate", fromDate.Date);
            AddParameter(cmd, "@ToDate", to); AddParameter(cmd, "@Skip", skip); AddParameter(cmd, "@Take", take);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) items.Add(MapReaderToAppointment(reader));
            return items;
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var result = new List<(int DoctorId, string DoctorName)>();
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveDoctorsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT StaffID AS DoctorId, FirstName + ' ' + LastName AS DoctorName FROM {table} ORDER BY FirstName;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add((GetInt(reader, "DoctorId"), GetString(reader, "DoctorName")));
            return result;
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveAppointmentsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE Id = @Id;";
            AddParameter(cmd, "@Id", appointmentId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new Appointment
            {
                Id = GetInt(reader, "Id"),
                DoctorId = GetInt(reader, "DoctorId"),
                PatientName = GetString(reader, "PatientName"),
                Date = GetDateTime(reader, "Date"),
                StartTime = GetTimeSpan(reader, "StartTime"),
                EndTime = GetTimeSpan(reader, "EndTime"),
                Status = GetNullableString(reader, "Status"),
                Type = GetNullableString(reader, "Type"),
                Location = GetNullableString(reader, "Location")
            };
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var items = new List<Appointment>();
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveAppointmentsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE DoctorId = @DoctorId ORDER BY [Date], StartTime;";
            AddParameter(cmd, "@DoctorId", doctorId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new Appointment
                {
                    Id = GetInt(reader, "Id"),
                    DoctorId = GetInt(reader, "DoctorId"),
                    PatientName = GetString(reader, "PatientName"),
                    Date = GetDateTime(reader, "Date"),
                    StartTime = GetTimeSpan(reader, "StartTime"),
                    EndTime = GetTimeSpan(reader, "EndTime"),
                    Status = GetNullableString(reader, "Status")
                });
            }
            return items;
        }

        public async Task AddAppointmentAsync(Appointment appt)
        {
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveAppointmentsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO {table} (PatientName, DoctorId, Date, StartTime, EndTime, Status) VALUES (@PatientName, @DoctorId, @Date, @StartTime, @EndTime, 'Scheduled')";

            AddParameter(cmd, "@PatientName", appt.PatientName); AddParameter(cmd, "@DoctorId", appt.DoctorId);
            AddParameter(cmd, "@Date", appt.Date.Date); AddParameter(cmd, "@StartTime", appt.StartTime); AddParameter(cmd, "@EndTime", appt.EndTime);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAppointmentStatusAsync(int id, string status)
        {
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveAppointmentsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {table} SET Status = @Status WHERE Id = @Id";
            AddParameter(cmd, "@Status", status); AddParameter(cmd, "@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
        {
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveAppointmentsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE DoctorId = @DocId AND Status = 'Scheduled'";
            AddParameter(cmd, "@DocId", doctorId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            using DbConnection conn = _dbManager.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            var table = await ResolveDoctorsTableAsync(conn);
            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {table} SET DoctorStatus = @Status WHERE id = @DocId";
            AddParameter(cmd, "@Status", status); AddParameter(cmd, "@DocId", doctorId);
            await cmd.ExecuteNonQueryAsync();
        }

   

        private Appointment MapReaderToAppointment(DbDataReader reader)
        {
            return new Appointment
            {
                Id = GetInt(reader, "Id"),
                DoctorId = GetInt(reader, "DoctorId"),
                DoctorName = GetNullableString(reader, "DoctorName"),
                PatientName = GetNullableString(reader, "PatientName"),
                Date = GetDateTime(reader, "Date"),
                StartTime = GetTimeSpan(reader, "StartTime"),
                EndTime = GetTimeSpan(reader, "EndTime"),
                Status = GetNullableString(reader, "Status"),
                Type = GetNullableString(reader, "Type"),
                Location = GetNullableString(reader, "Location")
            };
        }

        private async Task<string> ResolveDoctorsTableAsync(DbConnection conn)
        {
            var candidates = new[] { "[Doctors]", "[dbo].[Doctors]", "[doctor]" };
            foreach (var t in candidates)
                if (await TableExistsWithColumns(conn, t, "FirstName")) return t;
            return "Doctors"; 
        }

        private async Task<string> ResolveAppointmentsTableAsync(DbConnection conn)
        {
            var candidates = new[] { "[Appointments]", "[dbo].[Appointments]", "[appointment]" };
            foreach (var t in candidates)
                if (await TableExistsWithColumns(conn, t, "DoctorId", "PatientName")) return t;
            return "Appointments"; 
        }

        private async Task<bool> TableExistsWithColumns(DbConnection conn, string tableExpression, params string[] requiredColumns)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT TOP 0 * FROM {tableExpression};";
                using var reader = await cmd.ExecuteReaderAsync();

                var schema = reader.GetColumnSchema();
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in schema)
                    if (!string.IsNullOrWhiteSpace(c.ColumnName))
                        cols.Add(c.ColumnName!);

                foreach (var req in requiredColumns)
                    if (!cols.Contains(req)) return false;

                return true;
            }
            catch { return false; }
        }

        private void AddParameter(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private int GetInt(DbDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));
        private string GetString(DbDataReader r, string col) => r.GetString(r.GetOrdinal(col));
        private string GetNullableString(DbDataReader r, string col)
        {
            try { var i = r.GetOrdinal(col); return r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty; }
            catch { return string.Empty; }
        }
        private DateTime GetDateTime(DbDataReader r, string col)
        {
            var i = r.GetOrdinal(col); var v = r.GetValue(i);
            return v is DateTime dt ? dt : Convert.ToDateTime(v);
        }
        private TimeSpan GetTimeSpan(DbDataReader r, string col)
        {
            var i = r.GetOrdinal(col); var val = r.GetValue(i);
            if (val is TimeSpan ts) return ts;
            if (val is DateTime dt) return dt.TimeOfDay;
            return TimeSpan.Parse(val?.ToString() ?? "00:00:00");
        }
    }
}