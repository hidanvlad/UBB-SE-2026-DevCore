using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public interface IERDispatchService
    {
        Task<IReadOnlyList<int>> SimulateIncomingRequestsAsync(int count);
        Task<IReadOnlyList<int>> GetPendingRequestIdsAsync();
        Task<ERDispatchResult> DispatchERRequestAsync(int requestId);
        Task<ERDispatchResult> ManualOverrideAsync(int requestId, int doctorId, int nearEndMinutes);
        Task<IReadOnlyList<DoctorProfile>> GetManualOverrideCandidatesAsync(int requestId, int nearEndMinutes);
    }
}

