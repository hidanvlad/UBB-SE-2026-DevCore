using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.Configuration;

public static class AppSettings
{
    public const string ConnectionString =
        @"Data Source=PATRICKPC\SQLEXPRESS;Initial Catalog=HospitalDatabase;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    public const int DefaultDoctorId = 1;
}
