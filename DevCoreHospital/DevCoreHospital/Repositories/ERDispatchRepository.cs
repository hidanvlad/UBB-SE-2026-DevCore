using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ERDispatchRepository : IERDispatchRepository
    {
        private readonly string connectionString;
        private readonly ShiftSchemaCapabilities shiftSchema;

        public ERDispatchRepository(string? connectionString = null)
        {
            this.connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;
            shiftSchema = DetectShiftSchemaCapabilities();
        }

        public IReadOnlyList<DoctorRosterEntry> GetDoctorRoster()
            => FetchRosterEntries();

        public IReadOnlyList<ERRequest> GetPendingRequests()
            => FetchRequests();

        public int CreateIncomingRequest(string specialization, string location)
            => ExecuteCreateRequest(specialization, location, "PENDING");

        public ERRequest? GetRequestById(int requestId)
            => ExecuteGetRequestById(requestId);

        public DoctorRosterEntry? GetDoctorById(int doctorId)
            => FetchRosterEntriesByStaffId(doctorId).FirstOrDefault();

        public void UpdateRequestStatus(int requestId, string status, int? doctorId, string? doctorName)
            => ExecuteUpdateRequestStatus(requestId, status, doctorId, doctorName);

        public void UpdateDoctorStatus(int doctorId, DoctorStatus status)
            => ExecuteUpdateDoctorStatus(doctorId, status);

        protected virtual IReadOnlyList<DoctorRosterEntry> FetchRosterEntries()
        {
            var entries = new List<DoctorRosterEntry>();

            using (SqlConnection connection = new SqlConnection(connectionString))
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
                        {
                            entries.Add(ReadDoctorRosterEntry(reader));
                        }
                    }
                }
            }

            return entries;
        }

        protected virtual IReadOnlyList<DoctorRosterEntry> FetchRosterEntriesByStaffId(int staffId)
        {
            var entries = new List<DoctorRosterEntry>();

            using (SqlConnection connection = new SqlConnection(connectionString))
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
                        {
                            entries.Add(ReadDoctorRosterEntry(reader));
                        }
                    }
                }
            }

            return entries;
        }

        protected virtual IReadOnlyList<ERRequest> FetchRequests()
        {
            var requests = new List<ERRequest>();

            using (SqlConnection connection = new SqlConnection(connectionString))
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
                                AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6),
                            });
                        }
                    }
                }
            }

            return requests;
        }

        protected virtual int ExecuteCreateRequest(string specialization, string location, string status)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
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

                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        protected virtual ERRequest? ExecuteGetRequestById(int requestId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
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
                        {
                            return null;
                        }

                        return new ERRequest
                        {
                            Id = reader.GetInt32(0),
                            Specialization = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            Location = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            CreatedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                            Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            AssignedDoctorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        };
                    }
                }
            }
        }

        protected virtual void ExecuteUpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
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

        protected virtual void ExecuteUpdateDoctorStatus(int doctorId, DoctorStatus status)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
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
                ScheduleEnd = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            };
        }

        private string BuildShiftIsActiveProjection(string shiftAlias)
        {
            return shiftSchema.HasIsActive
                ? $"{shiftAlias}.is_active AS shift_is_active,"
                : "CAST(NULL AS bit) AS shift_is_active,";
        }

        private string BuildShiftStatusProjection(string shiftAlias)
        {
            return shiftSchema.HasStatus
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
            using (SqlConnection connection = new SqlConnection(connectionString))
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
