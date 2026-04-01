using DevCoreHospital.Models;
using System.Collections.Generic;

namespace DevCoreHospital.Data
{
    public interface IERDispatchDataSource
    {
        IReadOnlyList<DoctorRosterEntry> GetRosterEntries();
        IReadOnlyList<DoctorRosterEntry> GetRosterEntriesByStaffId(int staffId);
        IReadOnlyList<ERRequest> GetRequests();
        int CreateRequest(string specialization, string location, string status);
        ERRequest? GetRequestById(int requestId);
        void UpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName);
        void UpdateDoctorStatus(int doctorId, DoctorStatus status);
    }
}
