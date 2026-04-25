using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Repositories
{
    public class EvaluationsRepository : IEvaluationsRepository
    {
        private const string EvaluationSourcePatient = "PATIENT";
        private const int UnknownDoctorId = 0;

        private readonly string connectionString;

        public EvaluationsRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public EvaluationsRepository()
        {
            this.connectionString = AppSettings.ConnectionString;
        }

        public IReadOnlyList<MedicalEvaluation> GetAllEvaluations()
        {
            var evaluations = new List<MedicalEvaluation>();

            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "SELECT evaluation_id, doctor_id, patient_id, diagnosis, doctor_notes, medications FROM Medical_Evaluations;",
                connection);

            using SqlDataReader reader = command.ExecuteReader();
            int evaluationIdOrdinal = reader.GetOrdinal("evaluation_id");
            int doctorIdOrdinal = reader.GetOrdinal("doctor_id");
            int patientIdOrdinal = reader.GetOrdinal("patient_id");
            int diagnosisOrdinal = reader.GetOrdinal("diagnosis");
            int notesOrdinal = reader.GetOrdinal("doctor_notes");
            int medicationsOrdinal = reader.GetOrdinal("medications");

            while (reader.Read())
            {
                evaluations.Add(new MedicalEvaluation
                {
                    EvaluationID = reader.GetInt32(evaluationIdOrdinal),
                    PatientId = reader.IsDBNull(patientIdOrdinal) ? string.Empty : reader.GetInt32(patientIdOrdinal).ToString(),
                    Symptoms = reader.IsDBNull(diagnosisOrdinal) ? string.Empty : reader.GetString(diagnosisOrdinal),
                    Notes = reader.IsDBNull(notesOrdinal) ? string.Empty : reader.GetString(notesOrdinal),
                    MedicationsList = reader.IsDBNull(medicationsOrdinal) ? string.Empty : reader.GetString(medicationsOrdinal),
                    Evaluator = new Doctor { StaffID = reader.IsDBNull(doctorIdOrdinal) ? UnknownDoctorId : reader.GetInt32(doctorIdOrdinal) },
                });
            }
            return evaluations;
        }

        public void AddEvaluation(int doctorId, int patientId, string diagnosis, string notes, string medications, bool assumedRisk)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(@"
                INSERT INTO Medical_Evaluations
                (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
                VALUES (@DoctorId, @PatientId, @Diagnosis, @Notes, @Medications, @Source, @AssumedRisk);", connection);

            AddParameter(command, "@DoctorId", doctorId);
            AddParameter(command, "@PatientId", patientId);
            AddParameter(command, "@Diagnosis", diagnosis);
            AddParameter(command, "@Notes", notes);
            AddParameter(command, "@Medications", medications);
            AddParameter(command, "@Source", EvaluationSourcePatient);
            AddParameter(command, "@AssumedRisk", assumedRisk);

            command.ExecuteNonQuery();
        }

        public void DeleteEvaluation(int evaluationId)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlCommand command = new SqlCommand(
                "DELETE FROM Medical_Evaluations WHERE evaluation_id = @EvaluationId;", connection);
            AddParameter(command, "@EvaluationId", evaluationId);
            command.ExecuteNonQuery();
        }

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }
    }
}
