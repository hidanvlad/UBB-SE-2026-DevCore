using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IEvaluationsRepository
    {
        List<Doctor> GetAllDoctors();
        List<Appointment> GetAppointmentsByDoctor(int doctorId);
        List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId);
        void ExecuteSaveEvaluation(int doctorId, int patientId, string diagnosis, string notes, string meds, bool assumedRisk);
        void SaveEvaluation(MedicalEvaluation record);
        void DeleteEvaluation(int evaluationId);
        double GetDoctorFatigueHours(string doctorId);
        bool IsDoctorFatigued(string doctorId);
        string? CheckMedicineConflict(string patientId, string meds);
        Doctor? GetDoctorById(int id);
        string? GetHighRiskMedicineWarning(string medicineName);
        string? CheckPatientHistoryForRisk(string patientId, string currentMeds);
    }
}
