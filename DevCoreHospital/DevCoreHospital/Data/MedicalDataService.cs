using System.Collections.Generic;
using DevCoreHospital.Models;
using System.Linq;

namespace DevCoreHospital.Data
{
    public class MedicalDataService
    {
        
        private static List<MedicalEvaluation> _mockTable = new List<MedicalEvaluation>();

        // TASK 5: Save to Table
        public void SaveEvaluation(MedicalEvaluation record)
        {
            _mockTable.Add(record);
        }

        // TASK 6: SQL SELECT Query logic
        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            
            return _mockTable.Where(e => e.Evaluator != null && e.Evaluator.Id == doctorId).ToList();
        }
    }
}