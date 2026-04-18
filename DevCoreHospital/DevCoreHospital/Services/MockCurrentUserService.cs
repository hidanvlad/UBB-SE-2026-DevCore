using DevCoreHospital.Models;

namespace DevCoreHospital.Services;

public class MockCurrentUserService : ICurrentUserService
{
    public int UserId => 1;
    public UserRole RoleType { get; set; } = UserRole.Doctor;
    public string Role => RoleType.ToString();
}