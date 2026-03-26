namespace DevCoreHospital.Models;

/// <summary>Pharmacist row for handover selection (maps to PharmacyStaff).</summary>
public sealed class PharmacyStaffMember
{
    public int StaffId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
