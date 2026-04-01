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
                _shiftsMockTable.Add(new Shift(1, new DevCoreHospital.Models.Doctor(1, "Vlad", "Hidna", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE, 10), "Cardiology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.ACTIVE));
                _shiftsMockTable.Add(new Shift(2, new DevCoreHospital.Models.Doctor(2, "Alex", "Necs", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION, 15), "Neurology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.SCHEDULED));
            }
        }

        public string GetActivePatientId(int doctorId)
        {
            string sql = @"SELECT TOP 1 patient_id 
                           FROM Appointments 
                           WHERE doctor_id = @DocId AND status = 'Confirmed'
                           ORDER BY start_time DESC";

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

        public string? GetHighRiskMedicineWarning(string medicineName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string[] sqlVariants =
                {
                    "SELECT warning_message FROM High_Risk_Medicines WHERE medicine_name = @Name",
                    "SELECT WarningMessage FROM HighRiskMedicines WHERE MedicineName = @Name"
                };

                foreach (string sql in sqlVariants)
                {
                    try
                    {
                        using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@Name", medicineName.Trim());
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                    catch (SqlException)
                    {
                        // Try the next known schema variant.
                    }
                }

                return null;
            }
        }

        public void UpdateEvaluationNotes(int evaluationId, string newNotes)
        {
            string sql = "UPDATE Medical_Evaluations SET doctor_notes = @Notes WHERE evaluation_id = @Id";

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

        public void SaveEvaluation(MedicalEvaluation record)
        {
            string sql = @"INSERT INTO Medical_Evaluations 
                           (doctor_id, patient_id, diagnosis, doctor_notes, source, assumed_risk)
                           VALUES (@DocId, @PatId, @Diag, @Notes, @Source, @Risk)";

            int patientId = int.TryParse(record.PatientId, out var parsedPatientId) ? parsedPatientId : 0;
            bool assumedRisk = (record.Symptoms ?? string.Empty).IndexOf("[RISK]", StringComparison.OrdinalIgnoreCase) >= 0;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", record.Evaluator?.StaffID ?? 1);
                    cmd.Parameters.AddWithValue("@PatId", patientId);
                    cmd.Parameters.AddWithValue("@Diag", string.IsNullOrWhiteSpace(record.DiagnosisResult) ? record.Symptoms ?? string.Empty : record.DiagnosisResult);
                    cmd.Parameters.AddWithValue("@Notes", BuildDoctorNotes(record));
                    cmd.Parameters.AddWithValue("@Source", "PATIENT");
                    cmd.Parameters.AddWithValue("@Risk", assumedRisk);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            var results = new List<MedicalEvaluation>();
            string sql = @"SELECT evaluation_id, patient_id, diagnosis, doctor_notes
                           FROM Medical_Evaluations
                           WHERE doctor_id = @DocId
                           ORDER BY evaluation_id DESC";

            if (!int.TryParse(doctorId, out var parsedDoctorId))
            {
                return results;
            }

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", parsedDoctorId);
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

        public void DeleteEvaluation(int evaluationId)
        {
            string sql = "DELETE FROM Medical_Evaluations WHERE evaluation_id = @Id";

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
            string diagnosis = Convert.ToString(reader["diagnosis"]) ?? string.Empty;
            string notes = Convert.ToString(reader["doctor_notes"]) ?? string.Empty;

            return new MedicalEvaluation
            {
                EvaluationID = Convert.ToInt32(reader["evaluation_id"]),
                PatientId = Convert.ToString(reader["patient_id"]) ?? "0",
                Symptoms = diagnosis,
                MedsList = string.Empty,
                Notes = notes,
                EvaluationDate = DateTime.Now
            };
        }

        private static string BuildDoctorNotes(MedicalEvaluation record)
        {
            string notes = record.Notes ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(record.MedsList))
            {
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    notes += Environment.NewLine;
                }

                notes += $"Meds: {record.MedsList}";
            }

            return notes;
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
    }
}