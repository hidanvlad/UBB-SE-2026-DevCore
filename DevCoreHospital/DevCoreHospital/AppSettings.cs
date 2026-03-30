using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.Configuration;

public static class AppSettings
{
    public const string ConnectionString =
        @"Server=localhost\SQLEXPRESS;Database=HospitalDatabase;Trusted_Connection=True;TrustServerCertificate=True;";

    public const int DefaultDoctorId = 1;
}
