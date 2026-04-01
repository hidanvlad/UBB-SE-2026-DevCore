using DevCoreHospital.Models;
using System.Collections.Generic;

namespace DevCoreHospital.Repositories
{
    public interface IERDispatchRepository
    {
        IReadOnlyList<DoctorRosterEntry> GetDoctorRoster();
        IReadOnlyList<ERRequest> GetPendingRequests();
        int CreateIncomingRequest(string specialization, string location);
        ERRequest? GetRequestById(int requestId);
        DoctorRosterEntry? GetDoctorById(int doctorId);
        void UpdateRequestStatus(int requestId, string status, int? doctorId, string? doctorName);
        void UpdateDoctorStatus(int doctorId, DoctorStatus status);
    }
}

