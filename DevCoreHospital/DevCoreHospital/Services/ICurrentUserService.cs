namespace DevCoreHospital.Services;

public interface ICurrentUserService
{
    int UserId { get; }
    string Role { get; } // "Doctor", "Administrator", etc.
}