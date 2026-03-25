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

            var doctorsTable = await ResolveDoctorsTableAsync(conn);           // e.g. [dbo].[doctor]
            var appointmentsTable = await ResolveAppointmentsTableAsync(conn); // e.g. [dbo].[appointment]

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

        private static async Task<string> ResolveDoctorsTableAsync(DbConnection conn)
        {
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
            var table = (await cmd.ExecuteScalarAsync())?.ToString();

            if (string.IsNullOrWhiteSpace(table))
                throw new InvalidOperationException("Could not detect doctors table (expected columns: id, full_name, specialty).");

            return table!;
        }

        private static async Task<string> ResolveAppointmentsTableAsync(DbConnection conn)
        {
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
            var table = (await cmd.ExecuteScalarAsync())?.ToString();

            if (string.IsNullOrWhiteSpace(table))
                throw new InvalidOperationException("Could not detect appointments table (expected columns: id, doctor_id, date, start_time, end_time).");

            return table!;
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