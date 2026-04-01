namespace DevCoreHospital.Models
{
    public class Doctor : IStaff
    {
        public int StaffID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ContactInfo { get; set; }
        public bool Available { get; set; }
        public string Specialization { get; set; }
        public string LicenseNumber { get; set; }
        public int YearsOfExperience {  get; set; }
        public DoctorStatus DoctorStatus { get; set; }


        public Doctor() { }
        public Doctor(int staffID, string firstName, string lastName, string contactInfo, bool available,
            string specialization, string licenseNumber, DoctorStatus doctorStatus, int yearsOfExp)
        {
            this.StaffID = staffID;
            this.FirstName = firstName;
            this.LastName = lastName;
            this.ContactInfo = contactInfo;
            this.Available = available;
            this.Specialization = specialization;
            this.LicenseNumber = licenseNumber;
            this.DoctorStatus = doctorStatus;
            this.YearsOfExperience = yearsOfExp;
        }
    }
}