using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.Configuration;

public static class AppSettings
{
    public const string ConnectionString =
        @"Data Source=LAPTOP-UV77CFP3\SQLEXPRESS;Initial Catalog=HospitalDatabase;Integrated Security=True;Trust Server Certificate=True";

    public const int DefaultDoctorId = 1;
}