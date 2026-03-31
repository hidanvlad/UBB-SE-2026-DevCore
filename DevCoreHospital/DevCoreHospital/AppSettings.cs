using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.Configuration;

public static class AppSettings
{
    public const string ConnectionString =
        @"Data Source=ZENBOOK\SQLEXPRESS;Initial Catalog=DevCoreHospital;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    public const int DefaultDoctorId = 1;
}
