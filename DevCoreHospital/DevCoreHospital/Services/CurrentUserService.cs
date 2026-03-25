namespace DevCoreHospital.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    public int UserId { get; } = 1;
    public string Role { get; } = "Doctor";
}