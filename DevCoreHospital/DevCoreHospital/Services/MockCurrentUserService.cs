namespace DevCoreHospital.Services;

public class MockCurrentUserService : ICurrentUserService
{
    // Replace with real login context when available
    public int UserId => 1;
    public string Role => "Doctor";
}