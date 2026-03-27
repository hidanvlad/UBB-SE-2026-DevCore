namespace DevCoreHospital.Models
{
    public class Staff
    {
        public int StaffID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public bool Available { get; set; }
        public string LicenseNumber { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Certification { get; set; } = string.Empty;
        public int YearsOfExperience { get; set; }
        public double HourlyRate { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}