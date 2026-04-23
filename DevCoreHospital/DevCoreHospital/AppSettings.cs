using System;

namespace DevCoreHospital.Configuration;

public static class AppSettings
{
    public const string ConnectionString =
        @"Data Source=localhost\SQLEXPRESS;Initial Catalog=HospitalDatabase;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    public static int DefaultDoctorId { get; set; } = 1;

    public static readonly DateTime SqlMinimumDate = new DateTime(1753, 1, 1);
}



