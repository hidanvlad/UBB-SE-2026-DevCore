using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IEvaluationsRepository
    {
        IReadOnlyList<MedicalEvaluation> GetAllEvaluations();
        void AddEvaluation(int doctorId, int patientId, string diagnosis, string notes, string medications, bool assumedRisk);
        void DeleteEvaluation(int evaluationId);
    }
}
