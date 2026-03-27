using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services;

public interface IPharmacyHandoverService
{
    Task<int> GetProcessingQueueCountAsync(int responsibleStaffId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pharmacist>> GetAvailableIncomingPharmacistsAsync(
        int outgoingStaffId,
        CancellationToken cancellationToken = default);

    
    Task CompleteShiftHandoverAsync(int outgoingStaffId, int incomingStaffId, CancellationToken cancellationToken = default);
}
