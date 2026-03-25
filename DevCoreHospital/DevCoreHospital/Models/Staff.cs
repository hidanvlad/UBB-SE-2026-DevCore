namespace DevCoreHospital.Models
{
    public interface Staff
    {
        int staffID { get; set; }
        string firstName { get; set; }
        string lastName { get; set; }
        string contactInfo { get; set; }
        bool available { get; set; }

        public void UpdateAvailability(bool newAvailability);
    }
}