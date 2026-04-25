using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Integration
{
    public class SalaryComputationIntegrationTests
    {
        private const double DoctorBaseHourlyRate = 85.0;
        private const double PharmacistBaseHourlyRate = 45.0;
        private const double HangoutBonusMultiplier = 1.05;
        private const double MedicineSalesBonusRate = 0.05;
        private const double ShiftDurationHours = 8.0;
        private const int HandoverCountTriggeringBonus = 50;

        private sealed class InMemoryPharmacyHandoverRepository : IPharmacyHandoverRepository
        {
            public List<PharmacyHandover> Handovers { get; } = new();

            public IReadOnlyList<PharmacyHandover> GetAllPharmacyHandovers() => Handovers;
        }

        private sealed class InMemoryHangoutRepository : IHangoutRepository
        {
            public List<Hangout> Hangouts { get; } = new();

            public int AddHangout(string title, string description, DateTime date, int maxParticipants) => 0;

            public List<Hangout> GetAllHangouts() => Hangouts;

            public Hangout? GetHangoutById(int hangoutId) => null;
        }

        private sealed class InMemoryHangoutParticipantRepository : IHangoutParticipantRepository
        {
            public List<(int HangoutId, int StaffId)> Participants { get; } = new();

            public IReadOnlyList<(int HangoutId, int StaffId)> GetAllParticipants() => Participants;

            public void AddParticipant(int hangoutId, int staffId) => Participants.Add((hangoutId, staffId));
        }

        private static (SalaryComputationService Service,
            InMemoryPharmacyHandoverRepository HandoverRepo,
            InMemoryHangoutRepository HangoutRepo,
            InMemoryHangoutParticipantRepository ParticipantRepo) BuildStack()
        {
            var handoverRepository = new InMemoryPharmacyHandoverRepository();
            var hangoutRepository = new InMemoryHangoutRepository();
            var participantRepository = new InMemoryHangoutParticipantRepository();
            var service = new SalaryComputationService(handoverRepository, hangoutRepository, participantRepository);
            return (service, handoverRepository, hangoutRepository, participantRepository);
        }

        private static Shift MakeShift(int id, IStaff staff, DateTime start, DateTime end) =>
            new Shift(id, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);

        [Fact]
        public async Task ComputeSalaryDoctorAsync_WhenWeekdayDayShift_ReturnsBaseRateTimesHours()
        {
            var (service, _, _, _) = BuildStack();
            var doctor = new Doctor { StaffID = 1, Specialization = "Pediatrics", YearsOfExperience = 0 };
            var shift = MakeShift(10, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));

            double salary = await service.ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

            Assert.Equal(ShiftDurationHours * DoctorBaseHourlyRate, salary, 2);
        }

        [Fact]
        public async Task ComputeSalaryDoctorAsync_WhenStaffParticipatedInHangoutSameMonth_AppliesHangoutBonus()
        {
            var (service, _, hangoutRepository, participantRepository) = BuildStack();
            var doctor = new Doctor { StaffID = 1, Specialization = "Pediatrics", YearsOfExperience = 0 };
            var shift = MakeShift(10, doctor, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
            hangoutRepository.Hangouts.Add(new Hangout(7, "Lunch", "d", new DateTime(2026, 5, 10), 5));
            participantRepository.Participants.Add((7, 1));

            double salary = await service.ComputeSalaryDoctorAsync(doctor, new List<Shift> { shift }, 5, 2026);

            Assert.Equal(ShiftDurationHours * DoctorBaseHourlyRate * HangoutBonusMultiplier, salary, 2);
        }

        [Fact]
        public async Task ComputeSalaryPharmacistAsync_WhenHandoversInTargetMonth_AppliesMedicineSalesBonus()
        {
            var (service, handoverRepository, _, _) = BuildStack();
            var pharmacist = new Pharmacyst { StaffID = 5, YearsOfExperience = 0 };
            var shift = MakeShift(11, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));
            for (int handoverIndex = 0; handoverIndex < HandoverCountTriggeringBonus; handoverIndex++)
            {
                handoverRepository.Handovers.Add(new PharmacyHandover { PharmacistId = 5, HandoverDate = new DateTime(2026, 5, 1) });
            }

            double salary = await service.ComputeSalaryPharmacistAsync(pharmacist, new List<Shift> { shift }, 5, 2026);

            double baseSalary = ShiftDurationHours * PharmacistBaseHourlyRate;
            Assert.Equal(baseSalary + baseSalary * MedicineSalesBonusRate, salary, 2);
        }

        [Fact]
        public async Task ComputeSalaryPharmacistAsync_WhenNoHandovers_PaysOnlyBaseSalary()
        {
            var (service, _, _, _) = BuildStack();
            var pharmacist = new Pharmacyst { StaffID = 5, YearsOfExperience = 0 };
            var shift = MakeShift(11, pharmacist, new DateTime(2026, 5, 4, 8, 0, 0), new DateTime(2026, 5, 4, 16, 0, 0));

            double salary = await service.ComputeSalaryPharmacistAsync(pharmacist, new List<Shift> { shift }, 5, 2026);

            Assert.Equal(ShiftDurationHours * PharmacistBaseHourlyRate, salary, 2);
        }
    }
}