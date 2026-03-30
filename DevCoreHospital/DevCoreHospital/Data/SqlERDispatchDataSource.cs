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

        public SqlERDispatchDataSource(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;

            EnsureReq4Schema();
        }

        public IReadOnlyList<DoctorProfile> GetAvailableDoctors()
        {
            return GetDoctorsByStatus("AVAILABLE");
        }

        public IReadOnlyList<DoctorProfile> GetDoctorsInExamination()
        {
            return GetDoctorsByStatus("IN_EXAMINATION");
        }

        public IReadOnlyList<DoctorProfile> GetDoctorsNotWorkingNow()
        {
            var doctors = new List<DoctorProfile>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT s.staff_id,
                               s.first_name + ' ' + s.last_name AS full_name,
                               COALESCE(NULLIF(s.specialization, ''), 'General') AS specialization,
                               COALESCE(s.status, 'OFF_DUTY') AS doctor_status
                        FROM Staff s
                        WHERE UPPER(LTRIM(RTRIM(COALESCE(s.role, '')))) = 'DOCTOR'
                          AND UPPER(COALESCE(s.status, 'OFF_DUTY')) IN ('OFF_DUTY', 'AVAILABLE')
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM Shifts sh
                              WHERE sh.staff_id = s.staff_id
                                AND sh.start_time <= GETDATE()
                                AND sh.end_time >= GETDATE()
                                AND (sh.is_active = 1 OR UPPER(COALESCE(sh.status, '')) = 'ACTIVE')
                          );";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            doctors.Add(new DoctorProfile
                            {
                                DoctorId = reader.GetInt32(0),
                                FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Specialization = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = ParseStatus(reader.IsDBNull(3) ? null : reader.GetString(3)),
                                Location = string.Empty,
                                ScheduleStart = null,
                                ScheduleEnd = null
                            });
                        }
                    }
                }
            }

            return doctors;
        }

        public IReadOnlyList<ERRequest> GetPendingRequests()
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
                        WHERE UPPER(COALESCE(status, '')) = 'PENDING'
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
                                Status = reader.IsDBNull(4) ? "PENDING" : reader.GetString(4),
                                AssignedDoctorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                                AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return requests;
        }

        public int CreateIncomingRequest(string specialization, string location)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO dbo.ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
                        OUTPUT INSERTED.request_id
                        VALUES (@Specialization, @Location, GETDATE(), 'PENDING', NULL, NULL);";

                    AddParameter(command, "@Specialization", specialization);
                    AddParameter(command, "@Location", location);

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
                            Status = reader.IsDBNull(4) ? "PENDING" : reader.GetString(4),
                            AssignedDoctorId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            AssignedDoctorName = reader.IsDBNull(6) ? null : reader.GetString(6)
                        };
                    }
                }
            }
        }

        public DoctorProfile? GetDoctorById(int doctorId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT s.staff_id,
                               s.first_name + ' ' + s.last_name AS full_name,
                               COALESCE(NULLIF(s.specialization, ''), 'General') AS specialization,
                               COALESCE(s.status, 'OFF_DUTY') AS doctor_status,
                               COALESCE(sh.location, '') AS location,
                               sh.start_time,
                               sh.end_time
                        FROM Staff s
                        LEFT JOIN Shifts sh ON sh.staff_id = s.staff_id
                           AND sh.start_time <= GETDATE()
                           AND sh.end_time >= GETDATE()
                           AND (sh.is_active = 1 OR UPPER(COALESCE(sh.status, '')) = 'ACTIVE')
                        WHERE UPPER(LTRIM(RTRIM(COALESCE(s.role, '')))) = 'DOCTOR'
                          AND s.staff_id = @DoctorId;";
                    AddParameter(command, "@DoctorId", doctorId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new DoctorProfile
                        {
                            DoctorId = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            Specialization = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Status = ParseStatus(reader.IsDBNull(3) ? null : reader.GetString(3)),
                            Location = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            ScheduleStart = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            ScheduleEnd = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
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
                        WHERE staff_id = @DoctorId
                          AND UPPER(LTRIM(RTRIM(COALESCE(role, '')))) = 'DOCTOR';";
                    AddParameter(command, "@Status", status.ToString());
                    AddParameter(command, "@DoctorId", doctorId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private IReadOnlyList<DoctorProfile> GetDoctorsByStatus(string status)
        {
            var doctors = new List<DoctorProfile>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT s.staff_id,
                               s.first_name + ' ' + s.last_name AS full_name,
                               COALESCE(NULLIF(s.specialization, ''), 'General') AS specialization,
                               COALESCE(s.status, 'OFF_DUTY') AS doctor_status,
                               sh.location,
                               sh.start_time,
                               sh.end_time
                        FROM Staff s
                        INNER JOIN Shifts sh ON sh.staff_id = s.staff_id
                        WHERE UPPER(LTRIM(RTRIM(COALESCE(s.role, '')))) = 'DOCTOR'
                          AND UPPER(COALESCE(s.status, '')) = @Status
                          AND sh.start_time <= GETDATE()
                          AND sh.end_time >= GETDATE()
                          AND (sh.is_active = 1 OR UPPER(COALESCE(sh.status, '')) = 'ACTIVE');";
                    AddParameter(command, "@Status", status);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            doctors.Add(new DoctorProfile
                            {
                                DoctorId = reader.GetInt32(0),
                                FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Specialization = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = ParseStatus(reader.IsDBNull(3) ? null : reader.GetString(3)),
                                Location = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                ScheduleStart = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                                ScheduleEnd = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }

            return doctors;
        }

        private static DoctorStatus ParseStatus(string? raw)
        {
            var token = (raw ?? string.Empty).Trim().Replace(" ", "_");
            return Enum.TryParse<DoctorStatus>(token, true, out var status)
                ? status
                : DoctorStatus.OFF_DUTY;
        }

        private static void AddParameter(SqlCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private void EnsureReq4Schema()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        IF OBJECT_ID(N'dbo.ER_Requests', N'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.ER_Requests
                            (
                                request_id INT IDENTITY(101,1) PRIMARY KEY,
                                specialization VARCHAR(100) NOT NULL,
                                [location] VARCHAR(100) NOT NULL,
                                created_at DATETIME NOT NULL CONSTRAINT DF_ER_Requests_created_at DEFAULT GETDATE(),
                                [status] VARCHAR(50) NOT NULL,
                                assigned_doctor_id INT NULL,
                                assigned_doctor_name VARCHAR(200) NULL,
                                CONSTRAINT CK_ER_Requests_status CHECK (UPPER([status]) IN ('PENDING','ASSIGNED','UNMATCHED','COMPLETED')),
                                CONSTRAINT FK_ER_Requests_staff FOREIGN KEY (assigned_doctor_id) REFERENCES dbo.Staff(staff_id)
                            );
                        END;";

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}

