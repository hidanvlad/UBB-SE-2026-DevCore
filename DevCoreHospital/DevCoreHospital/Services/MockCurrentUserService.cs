using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public sealed class MockCurrentUserService : ICurrentUserService
    {
        public int UserId { get; set; } = 1;

        // keep old string role compatibility
        public string Role => RoleType.ToString();

        public UserRole RoleType { get; set; } = UserRole.Doctor;
    }
}