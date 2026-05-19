namespace DevCoreHospital.Models
{
    public class Pharmacyst : IStaff
    {
        public int StaffID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public bool Available { get; set; }
        public string Certification { get; set; } = string.Empty;
        public int YearsOfExperience { get; set; }

        public Pharmacyst()
        {
        }

        public Pharmacyst(int staffID, string firstName, string lastName, string contactInfo, bool available, string certification, int yearsOfExp)
        {
            this.StaffID = staffID;
            this.FirstName = firstName;
            this.LastName = lastName;
            this.ContactInfo = contactInfo;
            this.Available = available;
            this.Certification = certification;
            this.YearsOfExperience = yearsOfExp;
        }
    }
}