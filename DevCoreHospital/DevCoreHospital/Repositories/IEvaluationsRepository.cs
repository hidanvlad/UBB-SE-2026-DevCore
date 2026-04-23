using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IEvaluationsRepository
    {
        List<Doctor> GetAllDoctors();
        List<Appointment> GetAppointmentsByDoctor(int doctorId);
        List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId);
        void SaveEvaluation(MedicalEvaluation record);
        void DeleteEvaluation(int evaluationId);
        double GetDoctorFatigueHours(string doctorId);
        Doctor? GetDoctorById(int id);
        string? GetHighRiskMedicineWarning(string medicineName);
        string? CheckPatientHistoryForRisk(string patientId, string currentMeds);
    }
}
