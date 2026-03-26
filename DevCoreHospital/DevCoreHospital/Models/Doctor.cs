namespace DevCoreHospital.Models
{
    public enum DoctorStatus
    {
        AVAILABLE,
        IN_EXAMINATION,
        OFF_DUTY
    }

    public class Doctor : Staff
    {
        // --- UML fields ---
        public string? Specialization { get; set; }
        public string? LicenseNumber { get; set; }
        public DoctorStatus DoctorStatus { get; set; } = DoctorStatus.AVAILABLE;

        // --- Compatibility aliases for existing code paths ---
        // MedicalEvaluationViewModel previously set Doctor.Id/Name with staff-code identity.
        public string Id
        {
            get => StaffCode;
            set => StaffCode = value ?? string.Empty;
        }

        public string Name
        {
            get => DisplayName;
            set => DisplayName = value ?? string.Empty;
        }
    }
}