using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IMedicalEvaluationService
    {
        List<Doctor> GetAllDoctors();

        List<Appointment> GetAppointmentsByDoctor(int doctorId);

        List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId);

        void SaveEvaluation(MedicalEvaluation record);

        void DeleteEvaluation(int evaluationId);

        bool IsDoctorFatigued(string doctorId);

        string? CheckMedicineConflict(string patientId, string medications);
    }
}
