using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Data
{
    public sealed class MockERDispatchDataSource : IERDispatchDataSource
    {
        private readonly List<DoctorProfile> _doctors;
        private readonly List<ERRequest> _requests;

        public MockERDispatchDataSource()
        {
            var now = DateTime.Now;

            _doctors = new List<DoctorProfile>
            {
                new()
                {
                    DoctorId = 1,
                    FullName = "Dr. Mihai Pop",
                    Specialization = "Cardiology",
                    Status = DoctorStatus.OFF_DUTY,
                    Location = "ER_MAIN",
                    ScheduleStart = now.AddHours(-4),
                    ScheduleEnd = now.AddHours(4)
                },
                new()
                {
                    DoctorId = 2,
                    FullName = "Dr. Ana Ionescu",
                    Specialization = "Cardiology",
                    Status = DoctorStatus.IN_EXAMINATION,
                    Location = "ER_MAIN",
                    ScheduleStart = now.AddHours(-2),
                    ScheduleEnd = now.AddMinutes(20)
                },
                new()
                {
                    DoctorId = 3,
                    FullName = "Dr. Raul Petrescu",
                    Specialization = "ER",
                    Status = DoctorStatus.AVAILABLE,
                    Location = "ER_NORTH",
                    ScheduleStart = now.AddHours(-6),
                    ScheduleEnd = now.AddHours(2)
                },
                new()
                {
                    DoctorId = 4,
                    FullName = "Dr. Teodora Rusu",
                    Specialization = "Neurology",
                    Status = DoctorStatus.OFF_DUTY,
                    Location = "ER_MAIN",
                    ScheduleStart = now.AddHours(-8),
                    ScheduleEnd = now.AddHours(0)
                },
                new()
                {
                    DoctorId = 5,
                    FullName = "Dr. Vlad Ionescu",
                    Specialization = "Cardiology",
                    Status = DoctorStatus.AVAILABLE,
                    Location = "ER_SOUTH",
                    ScheduleStart = now.AddHours(-3),
                    ScheduleEnd = now.AddHours(5)
                }
            };

            _requests = new List<ERRequest>
            {
                new()
                {
                    Id = 101,
                    Specialization = "Cardiology",
                    Location = "ER_MAIN",
                    CreatedAt = now.AddMinutes(-15),
                    Status = "PENDING"
                },
                new()
                {
                    Id = 102,
                    Specialization = "Neurology",
                    Location = "ER_MAIN",
                    CreatedAt = now.AddMinutes(-5),
                    Status = "PENDING"
                },
                new()
                {
                    Id = 103,
                    Specialization = "ER",
                    Location = "ER_NORTH",
                    CreatedAt = now.AddMinutes(-2),
                    Status = "PENDING"
                }
            };
        }

        public IReadOnlyList<DoctorProfile> GetAvailableDoctors()
        {
            return _doctors
                .Where(d => d.Status == DoctorStatus.AVAILABLE)
                .ToList();
        }

        public IReadOnlyList<DoctorProfile> GetDoctorsInExamination()
        {
            return _doctors
                .Where(d => d.Status == DoctorStatus.IN_EXAMINATION)
                .ToList();
        }

        public IReadOnlyList<ERRequest> GetPendingRequests()
        {
            return _requests
                .Where(r => r.Status == "PENDING")
                .ToList();
        }

        public DoctorProfile? GetDoctorById(int doctorId)
        {
            return _doctors.FirstOrDefault(d => d.DoctorId == doctorId);
        }

        public ERRequest? GetRequestById(int requestId)
        {
            return _requests.FirstOrDefault(r => r.Id == requestId);
        }

        public void UpdateRequestStatus(int requestId, string status, int? assignedDoctorId, string? assignedDoctorName)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request != null)
            {
                request.Status = status;
                request.AssignedDoctorId = assignedDoctorId;
                request.AssignedDoctorName = assignedDoctorName;
            }
        }

        public void UpdateDoctorStatus(int doctorId, DoctorStatus status)
        {
            var doctor = _doctors.FirstOrDefault(d => d.DoctorId == doctorId);
            if (doctor != null)
                doctor.Status = status;
        }
    }
}

