using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        // Keep singleton-like shared state across pages
        private static UserRole _roleType = UserRole.Doctor;

        public int UserId { get; } = 1;

        public UserRole RoleType
        {
            get => _roleType;
            set => _roleType = value;
        }

        public string Role => RoleType.ToString();
    }
}