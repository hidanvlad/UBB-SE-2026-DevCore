using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Data
{
    public interface IERDispatchDataSource
    {
        IReadOnlyList<DoctorProfile> GetAvailableDoctors();
        IReadOnlyList<DoctorProfile> GetDoctorsInExamination();
        IReadOnlyList<DoctorProfile> GetDoctorsNotWorkingNow();
        IReadOnlyList<ERRequest> GetPendingRequests();
        int CreateIncomingRequest(string specialization, string location);
        ERRequest? GetRequestById(int requestId);
        DoctorProfile? GetDoctorById(int doctorId);
        void UpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName);
        void UpdateDoctorStatus(int doctorId, DoctorStatus status);
    }
}

