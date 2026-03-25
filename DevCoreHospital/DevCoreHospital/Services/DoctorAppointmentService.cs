using DevCoreHospital.Data;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class DoctorAppointmentService : IDoctorAppointmentService
    {
        private readonly SqlConnectionFactory _sqlFactory;

        public DoctorAppointmentService(SqlConnectionFactory sqlFactory)
        {
            _sqlFactory = sqlFactory;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            var items = new List<Appointment>();

            using DbConnection conn = _sqlFactory.Create();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var doctorsTable = await ResolveDoctorsTableAsync(conn);           // doctor / doctors / dbo.*
            var appointmentsTable = await ResolveAppointmentsTableAsync(conn); // appointment / appointments / dbo.*

            var sql = $@"
SELECT
    a.id AS Id,
    '' AS PatientName,
    a.doctor_id AS DoctorId,
    d.full_name AS DoctorName,
    CAST(a.[date] AS datetime2) AS [Date],
    a.start_time AS StartTime,
    a.end_time AS EndTime,
    a.status AS [Status],
    a.type AS [Type],
    a.location AS [Location],
    '' AS Notes
FROM {appointmentsTable} a
INNER JOIN {doctorsTable} d ON d.id = a.doctor_id
WHERE a.doctor_id = @DoctorId
  AND a.[date] >= @FromDate
ORDER BY a.[date], a.start_time
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameter(cmd, "@DoctorId", doctorUserId);
            AddParameter(cmd, "@FromDate", fromDate.Date);
            AddParameter(cmd, "@Skip", skip);
            AddParameter(cmd, "@Take", take);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new Appointment
                {
                    Id = GetInt(reader, "Id"),
                    PatientName = GetNullableString(reader, "PatientName"),
                    DoctorId = GetInt(reader, "DoctorId"),
                    DoctorName = GetString(reader, "DoctorName"),
                    Date = GetDateTime(reader, "Date"),
                    StartTime = GetTimeSpan(reader, "StartTime"),
                    EndTime = GetTimeSpan(reader, "EndTime"),
                    Status = GetNullableString(reader, "Status"),
                    Type = GetNullableString(reader, "Type"),
                    Location = GetNullableString(reader, "Location"),
                    Notes = GetNullableString(reader, "Notes")
                });
            }

            return items;
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var result = new List<(int DoctorId, string DoctorName)>();

            using DbConnection conn = _sqlFactory.Create();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var doctorsTable = await ResolveDoctorsTableAsync(conn);

            var sql = $@"
SELECT
    d.id AS DoctorId,
    d.full_name AS DoctorName
FROM {doctorsTable} d
ORDER BY d.full_name;";

            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add((GetInt(reader, "DoctorId"), GetString(reader, "DoctorName")));

            return result;
        }

        public async Task<AppointmentDetails?> GetAppointmentDetailsAsync(int appointmentId)
        {
            using DbConnection conn = _sqlFactory.Create();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var appointmentsTable = await ResolveAppointmentsTableAsync(conn);

            var sql = $@"
SELECT
    a.id,
    a.doctor_id,
    CAST(a.[date] AS datetime2) AS [date],
    a.start_time,
    a.end_time,
    a.status,
    a.type,
    a.location
FROM {appointmentsTable} a
WHERE a.id = @Id;";

            using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameter(cmd, "@Id", appointmentId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new AppointmentDetails
            {
                Id = GetInt(reader, "id"),
                DoctorId = GetInt(reader, "doctor_id"),
                Date = GetDateTime(reader, "date"),
                StartTime = GetTimeSpan(reader, "start_time"),
                EndTime = GetTimeSpan(reader, "end_time"),
                Status = GetNullableString(reader, "status"),
                Type = GetNullableString(reader, "type"),
                Location = GetNullableString(reader, "location")
            };
        }

        private static async Task<string> ResolveDoctorsTableAsync(DbConnection conn)
        {
            // strict candidates first (fast)
            var candidates = new[]
            {
                "[doctor]",
                "[doctors]",
                "[dbo].[doctor]",
                "[dbo].[doctors]"
            };

            foreach (var t in candidates)
            {
                if (await TableExistsWithColumns(conn, t, "id", "full_name"))
                    return t;
            }

            // metadata fallback
            const string sql = @"
SELECT TOP 1
    QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)
FROM INFORMATION_SCHEMA.COLUMNS c
GROUP BY c.TABLE_SCHEMA, c.TABLE_NAME
HAVING
    SUM(CASE WHEN c.COLUMN_NAME = 'id' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'full_name' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'specialty' THEN 1 ELSE 0 END) > 0;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var obj = await cmd.ExecuteScalarAsync();
            var table = obj?.ToString();

            if (!string.IsNullOrWhiteSpace(table))
                return table!;

            throw new InvalidOperationException("Could not find doctors table.");
        }

        private static async Task<string> ResolveAppointmentsTableAsync(DbConnection conn)
        {
            var candidates = new[]
            {
                "[appointment]",
                "[appointments]",
                "[dbo].[appointment]",
                "[dbo].[appointments]"
            };

            foreach (var t in candidates)
            {
                if (await TableExistsWithColumns(conn, t, "id", "doctor_id", "date", "start_time", "end_time"))
                    return t;
            }

            const string sql = @"
SELECT TOP 1
    QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)
FROM INFORMATION_SCHEMA.COLUMNS c
GROUP BY c.TABLE_SCHEMA, c.TABLE_NAME
HAVING
    SUM(CASE WHEN c.COLUMN_NAME = 'id' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'doctor_id' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'date' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'start_time' THEN 1 ELSE 0 END) > 0
    AND SUM(CASE WHEN c.COLUMN_NAME = 'end_time' THEN 1 ELSE 0 END) > 0;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var obj = await cmd.ExecuteScalarAsync();
            var table = obj?.ToString();

            if (!string.IsNullOrWhiteSpace(table))
                return table!;

            throw new InvalidOperationException("Could not find appointments table.");
        }

        private static async Task<bool> TableExistsWithColumns(DbConnection conn, string tableExpression, params string[] requiredColumns)
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
                    if (!cols.Contains(req))
                        return false;

                return true;
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name"))
            {
                return false;
            }
        }

        private static void AddParameter(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        private static int GetInt(DbDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));
        private static string GetString(DbDataReader r, string col) => r.GetString(r.GetOrdinal(col));

        private static string GetNullableString(DbDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;
        }

        private static DateTime GetDateTime(DbDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            var v = r.GetValue(i);
            return v switch
            {
                DateTime dt => dt,
                DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                _ => Convert.ToDateTime(v)
            };
        }

        private static TimeSpan GetTimeSpan(DbDataReader r, string col)
        {
            var i = r.GetOrdinal(col);
            var val = r.GetValue(i);
            return val switch
            {
                TimeSpan ts => ts,
                DateTime dt => dt.TimeOfDay,
                _ => TimeSpan.Parse(val?.ToString() ?? "00:00:00")
            };
        }
    }
}