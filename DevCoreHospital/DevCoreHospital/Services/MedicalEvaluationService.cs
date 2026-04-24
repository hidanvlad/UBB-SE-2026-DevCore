using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class MedicalEvaluationService : IMedicalEvaluationService
    {
        private readonly IEvaluationsRepository repository;

        public MedicalEvaluationService(IEvaluationsRepository repository)
        {
            this.repository = repository;
        }

        public List<Doctor> GetAllDoctors() => repository.GetAllDoctors();

        public List<Appointment> GetAppointmentsByDoctor(int doctorId) =>
            repository.GetAppointmentsByDoctor(doctorId);

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId) =>
            repository.GetEvaluationsByDoctor(doctorId);

        public void SaveEvaluation(MedicalEvaluation record) =>
            repository.SaveEvaluation(record);

        public void DeleteEvaluation(int evaluationId) =>
            repository.DeleteEvaluation(evaluationId);

        public bool IsDoctorFatigued(string doctorId) =>
            repository.IsDoctorFatigued(doctorId);

        public string? CheckMedicineConflict(string patientId, string meds) =>
            repository.CheckMedicineConflict(patientId, meds);
    }
}
