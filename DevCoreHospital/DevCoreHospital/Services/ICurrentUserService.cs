using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface ICurrentUserService
    {
        int UserId { get; }
        string Role { get; }
        UserRole RoleType { get; set; }
    }
}