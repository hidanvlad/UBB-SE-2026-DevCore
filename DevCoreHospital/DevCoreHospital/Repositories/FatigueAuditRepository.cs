using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class FatigueAuditRepository : IFatigueAuditRepository
    {
        private readonly string connectionString;

        public FatigueAuditRepository(string? connectionString = null)
        {
            this.connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;
        }

        public IReadOnlyList<RosterShift> GetAllShifts()
        {
            var shifts = new List<RosterShift>();

            using (SqlConnection connection = new SqlConnection(connectionString))
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
                               sh.end_time,
                               sh.status AS shift_status
                        FROM Shifts sh
                        INNER JOIN Staff st ON st.staff_id = sh.staff_id;";

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
                                End = reader.GetDateTime(6),
                                Status = reader.IsDBNull(7) ? null : reader.GetString(7),
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

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var staffSchema = GetStaffSchemaCapabilities(connection);

                using (SqlCommand command = connection.CreateCommand())
                {
                    var availabilityProjection = staffSchema.HasIsAvailable
                        ? "CAST(is_available AS bit) AS is_available"
                        : "CAST(NULL AS bit) AS is_available";
                    var activeProjection = staffSchema.HasIsActive
                        ? "CAST(is_active AS bit) AS is_active"
                        : "CAST(NULL AS bit) AS is_active";
                    var statusProjection = staffSchema.HasStatus
                        ? "[status] AS staff_status"
                        : "CAST(NULL AS nvarchar(50)) AS staff_status";

                    command.CommandText = $@"
                        SELECT staff_id,
                               first_name + ' ' + last_name AS full_name,
                               role,
                               COALESCE(NULLIF(specialization, ''), NULLIF(certification, ''), 'General') AS specialization,
                               {availabilityProjection},
                               {activeProjection},
                               {statusProjection}
                        FROM Staff;";

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
                                IsAvailable = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                                IsActive = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
                                Status = reader.IsDBNull(6) ? null : reader.GetString(6),
                            });
                        }
                    }
                }
            }

            return profiles;
        }

        public int UpdateShiftStaffId(int shiftId, int newStaffId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand update = connection.CreateCommand())
                {
                    update.CommandText = "UPDATE Shifts SET staff_id = @NewStaffId WHERE shift_id = @ShiftId;";
                    AddParameter(update, "@NewStaffId", newStaffId);
                    AddParameter(update, "@ShiftId", shiftId);

                    return update.ExecuteNonQuery();
                }
            }
        }

        private static StaffSchemaCapabilities GetStaffSchemaCapabilities(SqlConnection connection)
        {
            return new StaffSchemaCapabilities(
                ColumnExists(connection, "Staff", "is_active"),
                ColumnExists(connection, "Staff", "is_available"),
                ColumnExists(connection, "Staff", "status"));
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
