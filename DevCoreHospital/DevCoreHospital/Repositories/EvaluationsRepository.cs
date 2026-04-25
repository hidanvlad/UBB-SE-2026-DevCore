using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class EvaluationsRepository : IEvaluationsRepository
    {
        private readonly string connectionString;

        public EvaluationsRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public EvaluationsRepository()
        {
            this.connectionString = AppSettings.ConnectionString;
        }

        public List<Appointment> GetAppointmentsByDoctor(int doctorId)
        {
            var results = new List<Appointment>();
            string sql = @"SELECT appointment_id, patient_id, start_time, status
                           FROM Appointments
                           WHERE doctor_id = @DocId AND status = 'Confirmed'
                           ORDER BY start_time ASC";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@DocId", doctorId);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
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

        public virtual void ExecuteSaveEvaluation(int doctorId, int patientId, string diagnosis, string notes, string meds, bool assumedRisk)
        {
            string sql = @"INSERT INTO Medical_Evaluations
                           (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
                           VALUES (@DocId, @PatId, @Diag, @Notes, @Meds, @Source, @Risk)";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@DocId", doctorId);
                    command.Parameters.AddWithValue("@PatId", patientId);
                    command.Parameters.AddWithValue("@Diag", diagnosis);
                    command.Parameters.AddWithValue("@Notes", notes);
                    command.Parameters.AddWithValue("@Meds", meds);
                    command.Parameters.AddWithValue("@Source", "PATIENT");
                    command.Parameters.AddWithValue("@Risk", assumedRisk);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            if (!int.TryParse(doctorId, out var parsedDoctorId))
            {
                return new List<MedicalEvaluation>();
            }

            return ExecuteFetchEvaluationsByDoctor(parsedDoctorId);
        }

        protected virtual List<MedicalEvaluation> ExecuteFetchEvaluationsByDoctor(int parsedDoctorId)
        {
            var results = new List<MedicalEvaluation>();
            string sql = @"SELECT evaluation_id, patient_id, diagnosis, doctor_notes, medications
                           FROM Medical_Evaluations
                           WHERE doctor_id = @DocId
                           ORDER BY evaluation_id DESC";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@DocId", parsedDoctorId);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
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
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", evaluationId);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        public virtual double GetDoctorFatigueHours(string doctorId)
        {
            string sql = @"SELECT SUM(DATEDIFF(MINUTE, start_time, end_time)) / 60.0
                           FROM Shifts
                           WHERE staff_id = @DocId AND end_time >= DATEADD(day, -1, GETDATE())";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@DocId", doctorId);
                    connection.Open();
                    var result = command.ExecuteScalar();
                    return result != DBNull.Value ? Convert.ToDouble(result) : 0.0;
                }
            }
        }

        public Doctor? GetDoctorById(int id)
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand("SELECT staff_id, first_name, last_name FROM Staff WHERE staff_id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);
            connection.Open();
            using var reader = command.ExecuteReader();
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
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = "SELECT staff_id, first_name, last_name FROM Staff WHERE role = 'Doctor'";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
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

        public virtual string? GetHighRiskMedicineWarning(string medicineName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using var command = new SqlCommand("SELECT warning_message FROM High_Risk_Medicines WHERE medicine_name = @Name", connection);
                command.Parameters.AddWithValue("@Name", medicineName.Trim());
                var result = command.ExecuteScalar();
                return result?.ToString();
            }
        }

        public virtual string? CheckPatientHistoryForRisk(string patientId, string currentMeds)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = @"SELECT diagnosis, medications FROM Medical_Evaluations
                       WHERE patient_id = @PatId
                       AND (diagnosis LIKE '%Allergy%'
                            OR diagnosis LIKE '%Adverse%'
                            OR doctor_notes LIKE '%Allergy%'
                            OR doctor_notes LIKE '%Adverse%')";

                connection.Open();
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@PatId", patientId);
                    using (SqlDataReader reader = command.ExecuteReader())
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

        public bool IsDoctorFatigued(string doctorId)
        {
            throw new NotImplementedException("Use IMedicalEvaluationService.IsDoctorFatigued instead");
        }

        public string? CheckMedicineConflict(string patientId, string meds)
        {
            throw new NotImplementedException("Use IMedicalEvaluationService.CheckMedicineConflict instead");
        }

        public void SaveEvaluation(MedicalEvaluation record)
        {
            throw new NotImplementedException("Use IMedicalEvaluationService.SaveEvaluation instead");
        }
    }
}
