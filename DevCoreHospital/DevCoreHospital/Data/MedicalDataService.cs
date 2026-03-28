using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using DevCoreHospital.Models;

namespace DevCoreHospital.Data
{
    public class MedicalDataService
    {

        /// <summary>
        /// USE YOUR OWN CONNECTION HERE
        /// </summary>
        private readonly string _connectionString = @"Server=PATRICKPC\SQLEXPRESS;Database=DevCoreHospital;Trusted_Connection=True;TrustServerCertificate=True;";

        private static List<Shift> _shiftsMockTable = new List<Shift>();

        public MedicalDataService()
        {
            if (_shiftsMockTable.Count == 0)
            {
                _shiftsMockTable.Add(new Shift(1, new Doctor(1, "John", "Doe", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE), "Cardiology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.ACTIVE));
                _shiftsMockTable.Add(new Shift(2, new Doctor(2, "Jane", "Smith", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION), "Neurology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.SCHEDULED));
            }
        }

        public void UpdateEvaluationNotes(int evaluationId, string newNotes)
        {
            string sql = "UPDATE MedicalEvaluations SET Notes = @Notes WHERE EvaluationID = @Id";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Notes", newNotes);
                    cmd.Parameters.AddWithValue("@Id", evaluationId);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }


        public void SaveEvaluation(MedicalEvaluation record)
        {
            string sql = @"INSERT INTO MedicalEvaluations (PatientId, Symptoms, MedsList, Notes, EvaluationDate, DoctorId) 
                           VALUES (@PatientId, @Symptoms, @MedsList, @Notes, @Date, @DoctorId)";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientId", record.PatientId);
                    cmd.Parameters.AddWithValue("@Symptoms", record.Symptoms);
                    cmd.Parameters.AddWithValue("@MedsList", record.MedsList);
                    cmd.Parameters.AddWithValue("@Notes", record.Notes);
                    cmd.Parameters.AddWithValue("@Date", record.EvaluationDate);
                    cmd.Parameters.AddWithValue("@DoctorId", record.Evaluator?.StaffID ?? 0);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
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

        public void DeleteEvaluation(int evaluationId)
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

        // Helper method to keep domain logic and SQL mapping clean
        private MedicalEvaluation MapReaderToEvaluation(SqlDataReader reader)
        {
            return new MedicalEvaluation
            {
                EvaluationID = (int)reader["EvaluationID"],
                PatientId = reader["PatientId"].ToString(),
                Symptoms = reader["Symptoms"].ToString(),
                MedsList = reader["MedsList"].ToString(),
                Notes = reader["Notes"].ToString(),
                EvaluationDate = (DateTime)reader["EvaluationDate"],
                Evaluator = new Doctor { StaffID = (int)reader["DoctorId"] }
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