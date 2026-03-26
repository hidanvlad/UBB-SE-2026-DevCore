using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services;

public interface IPharmacyHandoverService
{
    /// <summary>Orders in Processing (not Completed) for this pharmacist.</summary>
    Task<int> GetProcessingQueueCountAsync(int responsibleStaffId, CancellationToken cancellationToken = default);

    /// <summary>Pharmacists available to receive handover (excludes outgoing).</summary>
    Task<IReadOnlyList<PharmacyStaffMember>> GetAvailableIncomingPharmacistsAsync(
        int outgoingStaffId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassigns Processing queue, marks outgoing unavailable, completes active shift.
    /// Throws if incoming is missing or invalid.
    /// </summary>
    Task CompleteShiftHandoverAsync(int outgoingStaffId, int incomingStaffId, CancellationToken cancellationToken = default);
}
