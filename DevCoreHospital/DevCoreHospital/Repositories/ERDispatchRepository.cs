using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class ERDispatchRepository : IERDispatchRepository
    {
        private readonly string connectionString;

        public ERDispatchRepository(string? connectionString = null)
        {
            this.connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? AppSettings.ConnectionString
                : connectionString;
        }

        public int AddRequest(string specialization, string location, string status)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO dbo.ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
                OUTPUT INSERTED.request_id
                VALUES (@Specialization, @Location, GETDATE(), @Status, NULL, NULL);", connection);

            AddParameter(command, "@Specialization", specialization);
            AddParameter(command, "@Location", location);
            AddParameter(command, "@Status", status);

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public IReadOnlyList<ERRequest> GetAllRequests()
        {
            var requests = new List<ERRequest>();

            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT request_id, specialization, location, created_at, status, assigned_doctor_id, assigned_doctor_name FROM dbo.ER_Requests;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int idOrdinal = reader.GetOrdinal("request_id");
            int specializationOrdinal = reader.GetOrdinal("specialization");
            int locationOrdinal = reader.GetOrdinal("location");
            int createdAtOrdinal = reader.GetOrdinal("created_at");
            int statusOrdinal = reader.GetOrdinal("status");
            int assignedDoctorIdOrdinal = reader.GetOrdinal("assigned_doctor_id");
            int assignedDoctorNameOrdinal = reader.GetOrdinal("assigned_doctor_name");

            while (reader.Read())
            {
                requests.Add(new ERRequest
                {
                    Id = reader.GetInt32(idOrdinal),
                    Specialization = reader.IsDBNull(specializationOrdinal) ? string.Empty : reader.GetString(specializationOrdinal),
                    Location = reader.IsDBNull(locationOrdinal) ? string.Empty : reader.GetString(locationOrdinal),
                    CreatedAt = reader.IsDBNull(createdAtOrdinal) ? DateTime.MinValue : reader.GetDateTime(createdAtOrdinal),
                    Status = reader.IsDBNull(statusOrdinal) ? string.Empty : reader.GetString(statusOrdinal),
                    AssignedDoctorId = reader.IsDBNull(assignedDoctorIdOrdinal) ? null : reader.GetInt32(assignedDoctorIdOrdinal),
                    AssignedDoctorName = reader.IsDBNull(assignedDoctorNameOrdinal) ? null : reader.GetString(assignedDoctorNameOrdinal),
                });
            }
            return requests;
        }

        public ERRequest? GetRequestById(int requestId)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT request_id, specialization, location, created_at, status, assigned_doctor_id, assigned_doctor_name FROM dbo.ER_Requests WHERE request_id = @RequestId;",
                connection);
            AddParameter(command, "@RequestId", requestId);

            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            int idOrdinal = reader.GetOrdinal("request_id");
            int specializationOrdinal = reader.GetOrdinal("specialization");
            int locationOrdinal = reader.GetOrdinal("location");
            int createdAtOrdinal = reader.GetOrdinal("created_at");
            int statusOrdinal = reader.GetOrdinal("status");
            int assignedDoctorIdOrdinal = reader.GetOrdinal("assigned_doctor_id");
            int assignedDoctorNameOrdinal = reader.GetOrdinal("assigned_doctor_name");

            return new ERRequest
            {
                Id = reader.GetInt32(idOrdinal),
                Specialization = reader.IsDBNull(specializationOrdinal) ? string.Empty : reader.GetString(specializationOrdinal),
                Location = reader.IsDBNull(locationOrdinal) ? string.Empty : reader.GetString(locationOrdinal),
                CreatedAt = reader.IsDBNull(createdAtOrdinal) ? DateTime.MinValue : reader.GetDateTime(createdAtOrdinal),
                Status = reader.IsDBNull(statusOrdinal) ? string.Empty : reader.GetString(statusOrdinal),
                AssignedDoctorId = reader.IsDBNull(assignedDoctorIdOrdinal) ? null : reader.GetInt32(assignedDoctorIdOrdinal),
                AssignedDoctorName = reader.IsDBNull(assignedDoctorNameOrdinal) ? null : reader.GetString(assignedDoctorNameOrdinal),
            };
        }

        public void UpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                UPDATE dbo.ER_Requests
                SET status = @Status,
                    assigned_doctor_id = @AssignedDoctorId,
                    assigned_doctor_name = @AssignedDoctorName
                WHERE request_id = @RequestId;", connection);

            AddParameter(command, "@Status", status);
            AddParameter(command, "@AssignedDoctorId", (object?)assignedDoctorId ?? DBNull.Value);
            AddParameter(command, "@AssignedDoctorName", (object?)assignedDoctorName ?? DBNull.Value);
            AddParameter(command, "@RequestId", requestId);
            command.ExecuteNonQuery();
        }

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
