using System;
using System.Collections.Generic;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class MedicalEvaluationService : IMedicalEvaluationService
    {
        private readonly IEvaluationsRepository repository;
        private const double FatigueThresholdHours = 12.0;

        public MedicalEvaluationService(IEvaluationsRepository repository)
        {
            this.repository = repository;
        }

        public List<Doctor> GetAllDoctors() => repository.GetAllDoctors();

        public List<Appointment> GetAppointmentsByDoctor(int doctorId) =>
            repository.GetAppointmentsByDoctor(doctorId);

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId) =>
            repository.GetEvaluationsByDoctor(doctorId);

        public void SaveEvaluation(MedicalEvaluation record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            int patientId = int.TryParse(record.PatientId, out var parsedPatientId) ? parsedPatientId : 0;
            bool assumedRisk = (record.Symptoms ?? string.Empty).IndexOf("[RISK]", StringComparison.OrdinalIgnoreCase) >= 0;
            int doctorId = record.Evaluator?.StaffID ?? AppSettings.DefaultDoctorId;

            repository.ExecuteSaveEvaluation(
                doctorId,
                patientId,
                record.Symptoms ?? string.Empty,
                record.Notes ?? string.Empty,
                record.MedsList ?? string.Empty,
                assumedRisk);
        }

        public void DeleteEvaluation(int evaluationId) =>
            repository.DeleteEvaluation(evaluationId);

        public bool IsDoctorFatigued(string doctorId) =>
            repository.GetDoctorFatigueHours(doctorId) >= FatigueThresholdHours;

        public string? CheckMedicineConflict(string patientId, string meds)
        {
            if (string.IsNullOrWhiteSpace(meds) || string.IsNullOrWhiteSpace(patientId))
            {
                return null;
            }

            string? highRiskWarning = repository.GetHighRiskMedicineWarning(meds);
            if (!string.IsNullOrEmpty(highRiskWarning))
            {
                return highRiskWarning;
            }

            return repository.CheckPatientHistoryForRisk(patientId, meds);
        }
    }
}
