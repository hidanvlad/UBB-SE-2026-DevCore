using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using DevCoreHospital.Models;
using DevCoreHospital.Configuration;

namespace DevCoreHospital.Repositories
{
    public class EvaluationsRepository
    {
        private readonly string _connectionString = AppSettings.ConnectionString;

        private static List<Shift> _shiftsMockTable = new List<Shift>();

        public EvaluationsRepository()
        {
            if (_shiftsMockTable.Count == 0)
            {
                _shiftsMockTable.Add(new Shift(1, new DevCoreHospital.Models.Doctor(1, "Vlad", "Hidna", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE), "Cardiology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.ACTIVE));
                _shiftsMockTable.Add(new Shift(2, new DevCoreHospital.Models.Doctor(2, "Alex", "Necs", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION), "Neurology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.SCHEDULED));
            }
        }

        public string GetActivePatientId(int doctorId)
        {
            string sql = @"SELECT TOP 1 PatientId 
                           FROM Appointments 
                           WHERE DoctorId = @DocId AND Status = 'Active' 
                           ORDER BY DateTime DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", doctorId);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "N/A";
                }
            }
        }

        public void updateEvaluationNotes(int evaluationId, string newNotes)
        {
            string sql = "UPDATE MedicalEvaluations SET Notes = @Notes WHERE EvaluationID = @Id";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Notes", newNotes ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Id", evaluationId);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void addEval(MedicalEvaluation record)
        {
            string sql = @"INSERT INTO MedicalEvaluations (PatientId, Symptoms, MedsList, Notes, EvaluationDate, DoctorId) 
                           VALUES (@PatientId, @Symptoms, @MedsList, @Notes, @Date, @DoctorId)";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientId", record.PatientId ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Symptoms", record.Symptoms ?? string.Empty);
                    cmd.Parameters.AddWithValue("@MedsList", record.MedsList ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Notes", record.Notes ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Date", record.EvaluationDate);
                    cmd.Parameters.AddWithValue("@DoctorId", record.Evaluator?.StaffID ?? 0);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MedicalEvaluation> loadEvals(string doctorId)
        {
            var results = new List<MedicalEvaluation>();
            string sql = "SELECT * FROM MedicalEvaluations WHERE DoctorId = @DocId ORDER BY EvaluationDate DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", doctorId);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(MapReaderToEvaluation(reader));
                        }
                    }
                }
            }
            return results;
        }

        public List<MedicalEvaluation> GetPatientMedicalHistory(string patientId)
        {
            var results = new List<MedicalEvaluation>();
            string sql = "SELECT * FROM MedicalEvaluations WHERE PatientId = @PatientId ORDER BY EvaluationDate DESC";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientId", patientId);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(MapReaderToEvaluation(reader));
                        }
                    }
                }
            }
            return results;
        }

        public string? GetHighRiskMedicineWarning(string medicineName)
        {
            string sql = "SELECT WarningMessage FROM HighRiskMedicines WHERE MedicineName = @Name";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", medicineName.Trim());
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        public void deleteEval(int evaluationId)
        {
            string sql = "DELETE FROM MedicalEvaluations WHERE EvaluationID = @Id";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", evaluationId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private MedicalEvaluation MapReaderToEvaluation(SqlDataReader reader)
        {
            return new MedicalEvaluation
            {
                EvaluationID = (int)reader["EvaluationID"],
                PatientId = reader["PatientId"]?.ToString() ?? string.Empty,
                Symptoms = reader["Symptoms"]?.ToString() ?? string.Empty,
                MedsList = reader["MedsList"]?.ToString() ?? string.Empty,
                Notes = reader["Notes"]?.ToString() ?? string.Empty,
                EvaluationDate = (DateTime)reader["EvaluationDate"],
                Evaluator = new DevCoreHospital.Models.Doctor { StaffID = (int)reader["DoctorId"] }
            };
        }

        public double GetDoctorFatigueHours(string doctorId) => CalculateMockFatigue(doctorId);

        private double CalculateMockFatigue(string doctorId)
        {
            var now = DateTime.Now;
            var dayAgo = now.AddHours(-24);
            var active = _shiftsMockTable.FirstOrDefault(s => s.AppointedStaff != null && s.AppointedStaff.StaffID.ToString() == doctorId && s.Status == ShiftStatus.ACTIVE);
            double activeHours = active != null ? (now - active.StartTime).TotalHours : 0;
            double completedHours = _shiftsMockTable
                .Where(s => s.AppointedStaff != null && s.AppointedStaff.StaffID.ToString() == doctorId && s.Status == ShiftStatus.COMPLETED && s.EndTime >= dayAgo)
                .Sum(s => (s.EndTime - s.StartTime).TotalHours);
            return activeHours + completedHours;
        }

        public void UpdateAppointmentStatus(string patientId, string status) { }
        public void UpdateDoctorAvailability(string doctorId) { }
        public void CreateAdminFatigueAlert(string doctorId) { }
    }
}