using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private const int DefaultLoggedInUserId = 1;

        private static UserRole roleType = UserRole.Doctor;

        public int UserId { get; } = DefaultLoggedInUserId;

        public UserRole RoleType
        {
            get => roleType;
            set => roleType = value;
        }

        public string Role => RoleType.ToString();
    }
}
