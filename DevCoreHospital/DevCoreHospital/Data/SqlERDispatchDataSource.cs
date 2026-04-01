using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace DevCoreHospital.Data
{
    public sealed class SqlERDispatchDataSource : IERDispatchDataSource
    {
        private readonly string _connectionString;
        private readonly ShiftSchemaCapabilities _shiftSchema;

        public SqlERDispatchDataSource(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;
            _shiftSchema = DetectShiftSchemaCapabilities();
        }

        public IReadOnlyList<DoctorRosterEntry> GetRosterEntries()
        {
            var entries = new List<DoctorRosterEntry>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                        SELECT s.staff_id,
                               s.first_name + ' ' + s.last_name AS full_name,
                               COALESCE(s.role, '') AS role_raw,
                               COALESCE(NULLIF(s.specialization, ''), '') AS specialization,
                               COALESCE(s.status, '') AS doctor_status,
                               COALESCE(sh.location, '') AS location,
                               {BuildShiftIsActiveProjection("sh")}
                               {BuildShiftStatusProjection("sh")}
                               sh.start_time,
                               sh.end_time
                        FROM Staff s
                        LEFT JOIN Shifts sh ON sh.staff_id = s.staff_id;";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            entries.Add(ReadDoctorRosterEntry(reader));
                    }
                }
            }

            return entries;
        }

        public IReadOnlyList<DoctorRosterEntry> GetRosterEntriesByStaffId(int staffId)
        {
            var entries = new List<DoctorRosterEntry>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                        SELECT s.staff_id,
                               s.first_name + ' ' + s.last_name AS full_name,
                               COALESCE(s.role, '') AS role_raw,
                               COALESCE(NULLIF(s.specialization, ''), '') AS specialization,
                               COALESCE(s.status, '') AS doctor_status,
                               COALESCE(sh.location, '') AS location,
                               {BuildShiftIsActiveProjection("sh")}
                               {BuildShiftStatusProjection("sh")}
                               sh.start_time,
                               sh.end_time
                        FROM Staff s
                        LEFT JOIN Shifts sh ON sh.staff_id = s.staff_id
                        WHERE s.staff_id = @StaffId;";
                    AddParameter(command, "@StaffId", staffId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            entries.Add(ReadDoctorRosterEntry(reader));
                    }
                }
            }

            return entries;
        }

        public IReadOnlyList<ERRequest> GetRequests()
        {
            var requests = new List<ERRequest>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT request_id, specialization, location, created_at, status, assigned_doctor_id, assigned_doctor_name
                        FROM dbo.ER_Requests
                        ORDER BY created_at;";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(new ERRequest
                            {
                                Id = reader.GetInt32(0),
                                Specialization = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Location = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                CreatedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                                Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                AssignedDoctorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                                AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return requests;
        }

        public int CreateRequest(string specialization, string location, string status)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO dbo.ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
                        OUTPUT INSERTED.request_id
                        VALUES (@Specialization, @Location, GETDATE(), @Status, NULL, NULL);";

                    AddParameter(command, "@Specialization", specialization);
                    AddParameter(command, "@Location", location);
                    AddParameter(command, "@Status", status);

                    return (int)command.ExecuteScalar()!;
                }
            }
        }

        public ERRequest? GetRequestById(int requestId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT request_id, specialization, location, created_at, status, assigned_doctor_id, assigned_doctor_name
                        FROM dbo.ER_Requests
                        WHERE request_id = @RequestId;";
                    AddParameter(command, "@RequestId", requestId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new ERRequest
                        {
                            Id = reader.GetInt32(0),
                            Specialization = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            Location = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            CreatedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                            Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            AssignedDoctorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6)
                        };
                    }
                }
            }
        }

        public void UpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE dbo.ER_Requests
                        SET status = @Status,
                            assigned_doctor_id = @AssignedDoctorId,
                            assigned_doctor_name = @AssignedDoctorName
                        WHERE request_id = @RequestId;";

                    AddParameter(command, "@Status", status);
                    AddParameter(command, "@AssignedDoctorId", (object?)assignedDoctorId ?? DBNull.Value);
                    AddParameter(command, "@AssignedDoctorName", (object?)assignedDoctorName ?? DBNull.Value);
                    AddParameter(command, "@RequestId", requestId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void UpdateDoctorStatus(int doctorId, DoctorStatus status)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE Staff
                        SET status = @Status
                        WHERE staff_id = @DoctorId;";
                    AddParameter(command, "@Status", status.ToString());
                    AddParameter(command, "@DoctorId", doctorId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static DoctorRosterEntry ReadDoctorRosterEntry(SqlDataReader reader)
        {
            return new DoctorRosterEntry
            {
                DoctorId = reader.GetInt32(0),
                FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                RoleRaw = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Specialization = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                StatusRaw = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Location = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                IsShiftActive = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
                ShiftStatusRaw = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                ScheduleStart = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                ScheduleEnd = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            };
        }

        private string BuildShiftIsActiveProjection(string shiftAlias)
        {
            return _shiftSchema.HasIsActive
                ? $"{shiftAlias}.is_active AS shift_is_active,"
                : "CAST(NULL AS bit) AS shift_is_active,";
        }

        private string BuildShiftStatusProjection(string shiftAlias)
        {
            return _shiftSchema.HasStatus
                ? $"COALESCE({shiftAlias}.status, '') AS shift_status,"
                : "CAST('' AS VARCHAR(50)) AS shift_status,";
        }

        private static void AddParameter(SqlCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private ShiftSchemaCapabilities DetectShiftSchemaCapabilities()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return new ShiftSchemaCapabilities(
                    ColumnExists(connection, "Shifts", "is_active"),
                    ColumnExists(connection, "Shifts", "status"));
            }
        }

        private static bool ColumnExists(SqlConnection connection, string tableName, string columnName)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
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



        private sealed class ShiftSchemaCapabilities
        {
            public ShiftSchemaCapabilities(bool hasIsActive, bool hasStatus)
            {
                HasIsActive = hasIsActive;
                HasStatus = hasStatus;
            }

            public bool HasIsActive { get; }
            public bool HasStatus { get; }
        }
    }
}

