using System.Linq;

namespace DevCoreHospital.Models;

public class Staff
{
    // --- UML fields ---
    public int StaffID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public bool Available { get; set; } = true;

    public void UpdateAvailability(bool newAvailability) => Available = newAvailability;

    // --- Compatibility aliases used across the existing app ---
    // Id is widely used as the in-memory numeric identifier.
    public int Id
    {
        get => StaffID;
        set => StaffID = value;
    }

    // Some parts of the app treat staff like a code-based identity (e.g. "DOC001"/"PHARM001").
    // Keep it while the DB/service layer still uses it.
    public string StaffCode
    {
        get => ContactInfo;
        set => ContactInfo = value ?? string.Empty;
    }

    // Existing UI binds to DisplayName; default it to "First Last" when present.
    public string DisplayName
    {
        get
        {
            var fn = FirstName?.Trim() ?? string.Empty;
            var ln = LastName?.Trim() ?? string.Empty;
            var full = string.Join(" ", new[] { fn, ln }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(full) ? _displayName : full;
        }
        set => _displayName = value ?? string.Empty;
    }
    private string _displayName = string.Empty;

    public string Role { get; set; } = string.Empty;

    // Kept because several repositories/services rely on it (and UML keeps specialization on Doctor).
    public string? Specialization { get; set; }

    public bool IsAvailable
    {
        get => Available;
        set => Available = value;
    }
}
