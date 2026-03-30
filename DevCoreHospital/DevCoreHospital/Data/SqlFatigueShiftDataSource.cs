using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Data
{
    public sealed class SqlFatigueShiftDataSource : IFatigueShiftDataSource
    {
        private const double MaxWeeklyHours = 60.0;
        private static readonly TimeSpan MinRestGap = TimeSpan.FromHours(12);

        private readonly string _connectionString;

        public SqlFatigueShiftDataSource(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;
        }

        public IReadOnlyList<RosterShift> GetShiftsForWeek(DateTime weekStart)
        {
            var shifts = new List<RosterShift>();
            var start = StartOfWeek(weekStart);
            var end = start.AddDays(7);

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT sh.shift_id,
                               sh.staff_id,
                               st.first_name + ' ' + st.last_name AS staff_name,
                               st.role,
                               COALESCE(NULLIF(st.specialization, ''), NULLIF(st.certification, ''), 'General') AS specialization,
                               sh.start_time,
                               sh.end_time
                        FROM Shifts sh
                        INNER JOIN Staff st ON st.staff_id = sh.staff_id
                        WHERE sh.start_time < @WeekEnd
                          AND sh.end_time > @WeekStart
                          AND UPPER(COALESCE(sh.status, '')) <> 'CANCELLED'
                        ORDER BY sh.start_time;";

                    AddParameter(command, "@WeekStart", start);
                    AddParameter(command, "@WeekEnd", end);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            shifts.Add(new RosterShift
                            {
                                Id = reader.GetInt32(0),
                                StaffId = reader.GetInt32(1),
                                StaffName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Role = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                Specialization = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                Start = reader.GetDateTime(5),
                                End = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }

            return shifts;
        }

        public IReadOnlyList<RosterShift> GetAllShifts()
        {
            var shifts = new List<RosterShift>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT sh.shift_id,
                               sh.staff_id,
                               st.first_name + ' ' + st.last_name AS staff_name,
                               st.role,
                               COALESCE(NULLIF(st.specialization, ''), NULLIF(st.certification, ''), 'General') AS specialization,
                               sh.start_time,
                               sh.end_time
                        FROM Shifts sh
                        INNER JOIN Staff st ON st.staff_id = sh.staff_id
                        WHERE UPPER(COALESCE(sh.status, '')) <> 'CANCELLED';";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            shifts.Add(new RosterShift
                            {
                                Id = reader.GetInt32(0),
                                StaffId = reader.GetInt32(1),
                                StaffName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Role = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                Specialization = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                Start = reader.GetDateTime(5),
                                End = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }

            return shifts;
        }

        public IReadOnlyList<StaffProfile> GetStaffProfiles()
        {
            var profiles = new List<StaffProfile>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var staffSchema = GetStaffSchemaCapabilities(connection);

                using (SqlCommand command = connection.CreateCommand())
                {
                    var whereClauses = new List<string>();
                    if (staffSchema.HasIsActive)
                        whereClauses.Add("ISNULL(is_active, 1) = 1");
                    if (staffSchema.HasStatus)
                        whereClauses.Add("UPPER(COALESCE([status], 'AVAILABLE')) <> 'INACTIVE'");

                    var availabilityProjection = staffSchema.HasIsAvailable
                        ? "CAST(ISNULL(is_available, 1) AS bit) AS is_available"
                        : "CAST(1 AS bit) AS is_available";
                    var whereSql = whereClauses.Count == 0
                        ? string.Empty
                        : $"WHERE {string.Join(" AND ", whereClauses)}";

                    command.CommandText = $@"
                        SELECT staff_id,
                               first_name + ' ' + last_name AS full_name,
                               role,
                               COALESCE(NULLIF(specialization, ''), NULLIF(certification, ''), 'General') AS specialization,
                               {availabilityProjection}
                        FROM Staff
                        {whereSql};";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            profiles.Add(new StaffProfile
                            {
                                StaffId = reader.GetInt32(0),
                                FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Role = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Specialization = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                IsAvailable = !reader.IsDBNull(4) && reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }

            return profiles;
        }

        public double GetMonthlyWorkedHours(int staffId, int year, int month)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        DECLARE @MonthStart DATETIME = DATETIMEFROMPARTS(@Year, @Month, 1, 0, 0, 0, 0);
                        DECLARE @MonthEnd DATETIME = DATEADD(MONTH, 1, @MonthStart);

                        SELECT COALESCE(SUM(
                                   DATEDIFF(MINUTE,
                                       CASE WHEN start_time < @MonthStart THEN @MonthStart ELSE start_time END,
                                       CASE WHEN end_time > @MonthEnd THEN @MonthEnd ELSE end_time END)
                               ), 0)
                        FROM Shifts
                        WHERE staff_id = @StaffId
                          AND start_time < @MonthEnd
                          AND end_time > @MonthStart
                          AND UPPER(COALESCE(status, '')) <> 'CANCELLED';";

                    AddParameter(command, "@StaffId", staffId);
                    AddParameter(command, "@Year", year);
                    AddParameter(command, "@Month", month);

                    var totalMinutes = Convert.ToInt32(command.ExecuteScalar());
                    return totalMinutes / 60.0;
                }
            }
        }

        public bool ReassignShift(int shiftId, int newStaffId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        DateTime? shiftStart = null;
                        DateTime? shiftEnd = null;

                        using (SqlCommand loadShift = connection.CreateCommand())
                        {
                            loadShift.Transaction = transaction;
                            loadShift.CommandText = "SELECT start_time, end_time FROM Shifts WHERE shift_id = @ShiftId;";
                            AddParameter(loadShift, "@ShiftId", shiftId);

                            using (SqlDataReader reader = loadShift.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    shiftStart = reader.GetDateTime(0);
                                    shiftEnd = reader.GetDateTime(1);
                                }
                            }
                        }

                        if (!shiftStart.HasValue || !shiftEnd.HasValue)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        var staffSchema = GetStaffSchemaCapabilities(connection, transaction);

                        using (SqlCommand candidateCheck = connection.CreateCommand())
                        {
                            candidateCheck.Transaction = transaction;

                            var candidateFilters = new List<string> { "staff_id = @NewStaffId" };
                            if (staffSchema.HasIsActive)
                                candidateFilters.Add("ISNULL(is_active, 1) = 1");
                            if (staffSchema.HasIsAvailable)
                                candidateFilters.Add("ISNULL(is_available, 1) = 1");
                            if (staffSchema.HasStatus)
                                candidateFilters.Add("UPPER(COALESCE([status], 'AVAILABLE')) <> 'INACTIVE'");

                            candidateCheck.CommandText = $@"
                                SELECT COUNT(*)
                                FROM Staff
                                WHERE {string.Join(" AND ", candidateFilters)};";
                            AddParameter(candidateCheck, "@NewStaffId", newStaffId);

                            var candidateCount = Convert.ToInt32(candidateCheck.ExecuteScalar());
                            if (candidateCount == 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        var existingShifts = new List<(DateTime Start, DateTime End)>();
                        using (SqlCommand candidateShiftsCommand = connection.CreateCommand())
                        {
                            candidateShiftsCommand.Transaction = transaction;
                            candidateShiftsCommand.CommandText = @"
                                SELECT start_time, end_time
                                FROM Shifts
                                WHERE staff_id = @NewStaffId
                                  AND shift_id <> @ShiftId
                                  AND UPPER(COALESCE(status, '')) <> 'CANCELLED';";
                            AddParameter(candidateShiftsCommand, "@NewStaffId", newStaffId);
                            AddParameter(candidateShiftsCommand, "@ShiftId", shiftId);

                            using (SqlDataReader reader = candidateShiftsCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    existingShifts.Add((reader.GetDateTime(0), reader.GetDateTime(1)));
                                }
                            }
                        }

                        using (SqlCommand overlapCheck = connection.CreateCommand())
                        {
                            overlapCheck.Transaction = transaction;
                            overlapCheck.CommandText = @"
                                SELECT COUNT(*)
                                FROM Shifts
                                WHERE staff_id = @NewStaffId
                                  AND shift_id <> @ShiftId
                                  AND UPPER(COALESCE(status, '')) IN ('SCHEDULED', 'ACTIVE')
                                  AND start_time < @ShiftEnd
                                  AND end_time > @ShiftStart;";

                            AddParameter(overlapCheck, "@NewStaffId", newStaffId);
                            AddParameter(overlapCheck, "@ShiftId", shiftId);
                            AddParameter(overlapCheck, "@ShiftStart", shiftStart.Value);
                            AddParameter(overlapCheck, "@ShiftEnd", shiftEnd.Value);

                            var overlapCount = Convert.ToInt32(overlapCheck.ExecuteScalar());
                            if (overlapCount > 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        if (!RespectsRestGap(shiftStart.Value, shiftEnd.Value, existingShifts))
                        {
                            transaction.Rollback();
                            return false;
                        }

                        var weekStart = StartOfWeek(shiftStart.Value);
                        var weekEnd = weekStart.AddDays(7);
                        var existingHours = existingShifts.Sum(s => GetOverlapHours(s.Start, s.End, weekStart, weekEnd));
                        var reassignedHours = GetOverlapHours(shiftStart.Value, shiftEnd.Value, weekStart, weekEnd);
                        if (existingHours + reassignedHours > MaxWeeklyHours)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        using (SqlCommand update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = "UPDATE Shifts SET staff_id = @NewStaffId WHERE shift_id = @ShiftId;";
                            AddParameter(update, "@NewStaffId", newStaffId);
                            AddParameter(update, "@ShiftId", shiftId);

                            var rows = update.ExecuteNonQuery();
                            transaction.Commit();
                            return rows > 0;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }

        private static StaffSchemaCapabilities GetStaffSchemaCapabilities(SqlConnection connection, SqlTransaction? transaction = null)
        {
            return new StaffSchemaCapabilities(
                ColumnExists(connection, transaction, "Staff", "is_active"),
                ColumnExists(connection, transaction, "Staff", "is_available"),
                ColumnExists(connection, transaction, "Staff", "status"));
        }

        private static bool ColumnExists(SqlConnection connection, SqlTransaction? transaction, string tableName, string columnName)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                      AND COLUMN_NAME = @ColumnName;";

                AddParameter(command, "@TableName", tableName);
                AddParameter(command, "@ColumnName", columnName);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static bool RespectsRestGap(DateTime proposedStart, DateTime proposedEnd, IReadOnlyList<(DateTime Start, DateTime End)> existingShifts)
        {
            var previousShift = existingShifts
                .Where(s => s.End <= proposedStart)
                .OrderByDescending(s => s.End)
                .FirstOrDefault();
            if (previousShift != default && (proposedStart - previousShift.End) < MinRestGap)
                return false;

            var nextShift = existingShifts
                .Where(s => s.Start >= proposedEnd)
                .OrderBy(s => s.Start)
                .FirstOrDefault();
            if (nextShift != default && (nextShift.Start - proposedEnd) < MinRestGap)
                return false;

            return true;
        }

        private static double GetOverlapHours(DateTime shiftStart, DateTime shiftEnd, DateTime windowStart, DateTime windowEnd)
        {
            var overlapStart = shiftStart > windowStart ? shiftStart : windowStart;
            var overlapEnd = shiftEnd < windowEnd ? shiftEnd : windowEnd;
            return overlapEnd <= overlapStart ? 0 : (overlapEnd - overlapStart).TotalHours;
        }

        private static void AddParameter(SqlCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private sealed class StaffSchemaCapabilities
        {
            public StaffSchemaCapabilities(bool hasIsActive, bool hasIsAvailable, bool hasStatus)
            {
                HasIsActive = hasIsActive;
                HasIsAvailable = hasIsAvailable;
                HasStatus = hasStatus;
            }

            public bool HasIsActive { get; }
            public bool HasIsAvailable { get; }
            public bool HasStatus { get; }
        }
    }
}

