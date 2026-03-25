using System;
using System.Numerics;

namespace DevCoreHospital.Models
{
    public class MedicalEvaluation
    {
        
        public string Id { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string MedsList { get; set; } = string.Empty;
        public string DoctorNotes { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; }
        public Doctor? Evaluator { get; set; }


        public string FormattedDate => EvaluationDate.ToString("dd MMM yyyy");
        public string FormattedTime => EvaluationDate.ToString("HH:mm");
    }
}