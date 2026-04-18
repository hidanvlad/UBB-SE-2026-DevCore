using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class EvaluationsRepository
    {
        private readonly string connectionString = AppSettings.ConnectionString;

        public List<Appointment> GetAppointmentsByDoctor(int doctorId)
        {
            var results = new List<Appointment>();
            string sql = @"SELECT appointment_id, patient_id, start_time, status 
                           FROM Appointments 
                           WHERE doctor_id = @DocId AND status = 'Confirmed'
                           ORDER BY start_time ASC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", doctorId);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime fullDateTime = reader.GetDateTime(reader.GetOrdinal("start_time"));
                            results.Add(new Appointment
                            {
                                Id = Convert.ToInt32(reader["appointment_id"]),
                                PatientName = "Patient ID: " + reader["patient_id"].ToString(),
                                Notes = reader["patient_id"].ToString(),
                                StartTime = fullDateTime.TimeOfDay,
                                Status = reader["status"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public void SaveEvaluation(MedicalEvaluation record)
        {
            string sql = @"INSERT INTO Medical_Evaluations 
                           (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
                           VALUES (@DocId, @PatId, @Diag, @Notes, @Meds, @Source, @Risk)";

            int patientId = int.TryParse(record.PatientId, out var parsedPatientId) ? parsedPatientId : 0;
            bool assumedRisk = (record.Symptoms ?? string.Empty).IndexOf("[RISK]", StringComparison.OrdinalIgnoreCase) >= 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", record.Evaluator?.StaffID ?? AppSettings.DefaultDoctorId);
                    cmd.Parameters.AddWithValue("@PatId", patientId);
                    cmd.Parameters.AddWithValue("@Diag", record.Symptoms ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Notes", record.Notes ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Meds", record.MedsList ?? string.Empty);
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
            string sql = @"SELECT evaluation_id, patient_id, diagnosis, doctor_notes, medications
                           FROM Medical_Evaluations
                           WHERE doctor_id = @DocId
                           ORDER BY evaluation_id DESC";

            if (!int.TryParse(doctorId, out var parsedDoctorId))
            {
                return results;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
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

        private MedicalEvaluation MapReaderToEvaluation(SqlDataReader reader)
        {
            return new MedicalEvaluation
            {
                EvaluationID = Convert.ToInt32(reader["evaluation_id"]),
                PatientId = Convert.ToString(reader["patient_id"]) ?? "0",
                Symptoms = Convert.ToString(reader["diagnosis"]) ?? string.Empty,
                MedsList = Convert.ToString(reader["medications"]) ?? string.Empty,
                Notes = Convert.ToString(reader["doctor_notes"]) ?? string.Empty,
                EvaluationDate = DateTime.Now
            };
        }

        public void DeleteEvaluation(int evaluationId)
        {
            string sql = "DELETE FROM Medical_Evaluations WHERE evaluation_id = @Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", evaluationId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public double GetDoctorFatigueHours(string doctorId)
        {
            string sql = @"SELECT SUM(DATEDIFF(MINUTE, start_time, end_time)) / 60.0 
                           FROM Shifts 
                           WHERE staff_id = @DocId AND end_time >= DATEADD(day, -1, GETDATE())";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DocId", doctorId);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? Convert.ToDouble(result) : 0.0;
                }
            }
        }

        public DevCoreHospital.Models.Doctor? GetDoctorById(int id)
        {
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand("SELECT staff_id, first_name, last_name FROM Staff WHERE staff_id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new DevCoreHospital.Models.Doctor(
                    Convert.ToInt32(reader["staff_id"]),
                    reader["first_name"].ToString() ?? string.Empty,
                    reader["last_name"].ToString() ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    true,
                    string.Empty,
                    "Available",
                    DoctorStatus.AVAILABLE,
                    0);
            }
            return null;
        }

        public List<DevCoreHospital.Models.Doctor> GetAllDoctors()
        {
            var doctors = new List<DevCoreHospital.Models.Doctor>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = "SELECT staff_id, first_name, last_name FROM Staff WHERE role = 'Doctor'";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            doctors.Add(new DevCoreHospital.Models.Doctor(
                                Convert.ToInt32(reader["staff_id"]),
                                reader["first_name"].ToString() ?? string.Empty,
                                reader["last_name"].ToString() ?? string.Empty,
                                string.Empty,
                                string.Empty,
                                true,
                                string.Empty,
                                "Available",
                                DoctorStatus.AVAILABLE,
                                0));
                        }
                    }
                }
            }
            return doctors;
        }

        public string? GetHighRiskMedicineWarning(string medicineName)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using var cmd = new SqlCommand("SELECT warning_message FROM High_Risk_Medicines WHERE medicine_name = @Name", conn);
                cmd.Parameters.AddWithValue("@Name", medicineName.Trim());
                var result = cmd.ExecuteScalar();
                return result?.ToString();
            }
        }

        public string? CheckPatientHistoryForRisk(string patientId, string currentMeds)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = @"SELECT diagnosis, medications FROM Medical_Evaluations 
                       WHERE patient_id = @PatId 
                       AND (diagnosis LIKE '%Allergy%' 
                            OR diagnosis LIKE '%Adverse%' 
                            OR doctor_notes LIKE '%Allergy%' 
                            OR doctor_notes LIKE '%Adverse%')";

                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PatId", patientId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string pastMeds = reader["medications"].ToString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(currentMeds) && pastMeds.Contains(currentMeds, StringComparison.OrdinalIgnoreCase))
                            {
                                return $"HISTORY ALERT: Patient had a past Adverse Reaction/Allergy to {currentMeds} recorded in their history.";
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}