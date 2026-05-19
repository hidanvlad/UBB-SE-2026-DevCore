using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Integration
{
    public class HangoutFlowIntegrationTests
    {
        private static readonly DateTime FutureHangoutDate = DateTime.Now.Date.AddDays(14);

        private sealed class InMemoryHangoutRepository : IHangoutRepository
        {
            private readonly List<Hangout> hangouts = new();
            private int nextId = 1;

            public IReadOnlyList<Hangout> Stored => hangouts;

            public int AddHangout(string title, string description, DateTime date, int maxParticipants)
            {
                int assignedId = nextId++;
                hangouts.Add(new Hangout(assignedId, title, description, date, maxParticipants));
                return assignedId;
            }

            public List<Hangout> GetAllHangouts() => hangouts.ToList();

            public Hangout? GetHangoutById(int hangoutId)
            {
                bool HasMatchingId(Hangout hangout) => hangout.HangoutID == hangoutId;
                return hangouts.FirstOrDefault(HasMatchingId);
            }
        }

        private sealed class InMemoryHangoutParticipantRepository : IHangoutParticipantRepository
        {
            private readonly List<(int HangoutId, int StaffId)> participants = new();

            public IReadOnlyList<(int HangoutId, int StaffId)> GetAllParticipants() => participants;

            public void AddParticipant(int hangoutId, int staffId) => participants.Add((hangoutId, staffId));
        }

        private sealed class StubAppointmentRepository : IAppointmentRepository
        {
            public IReadOnlyList<Appointment> Appointments { get; set; } = new List<Appointment>();

            public Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync() => Task.FromResult(Appointments);

            public Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime, string status)
                => Task.CompletedTask;

            public Task UpdateAppointmentStatusAsync(int id, string status) => Task.CompletedTask;
        }

        private sealed class StubStaffRepository : IStaffRepository
        {
            public List<IStaff> Members { get; } = new();

            public List<IStaff> LoadAllStaff() => Members;

            public IStaff? GetStaffById(int staffId)
            {
                bool HasMatchingId(IStaff member) => member.StaffID == staffId;
                return Members.FirstOrDefault(HasMatchingId);
            }

            public Task<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>> GetAllDoctorsAsync()
                => Task.FromResult<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>>(new List<(int, string, string)>());

            public Task UpdateStatusAsync(int staffId, string status) => Task.CompletedTask;
        }

        private static (HangoutService Service, InMemoryHangoutRepository HangoutRepo,
            InMemoryHangoutParticipantRepository ParticipantRepo, StubAppointmentRepository AppointmentRepo,
            StubStaffRepository StaffRepo) BuildStack()
        {
            var hangoutRepository = new InMemoryHangoutRepository();
            var participantRepository = new InMemoryHangoutParticipantRepository();
            var appointmentRepository = new StubAppointmentRepository();
            var staffRepository = new StubStaffRepository();
            var service = new HangoutService(hangoutRepository, participantRepository, appointmentRepository, staffRepository);
            return (service, hangoutRepository, participantRepository, appointmentRepository, staffRepository);
        }

        private static Doctor MakeDoctor(int staffId, string firstName = "Ana", string lastName = "Pop") =>
            new Doctor(staffId, firstName, lastName, string.Empty, true, "Cardiology", "LIC", DoctorStatus.AVAILABLE, 3);

        [Fact]
        public void CreateHangout_WhenInputValid_PersistsHangoutInRepository()
        {
            var (service, hangoutRepository, _, _, _) = BuildStack();

            service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, MakeDoctor(1));

            Assert.Single(hangoutRepository.Stored);
        }

        [Fact]
        public void CreateHangout_WhenInputValid_AddsCreatorAsParticipant()
        {
            var (service, _, participantRepository, _, _) = BuildStack();

            service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, MakeDoctor(7));

            Assert.Equal((1, 7), participantRepository.GetAllParticipants().Single());
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasActiveAppointmentOnSameDay_ThrowsInvalidOperationException()
        {
            var (service, _, _, appointmentRepository, _) = BuildStack();
            appointmentRepository.Appointments = new List<Appointment>
            {
                new Appointment { DoctorId = 1, Date = FutureHangoutDate, Status = "Scheduled" },
            };

            Assert.Throws<InvalidOperationException>(() =>
                service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, MakeDoctor(1)));
        }

        [Fact]
        public void JoinHangout_WhenSecondStaffJoins_RecordsBothParticipants()
        {
            var (service, _, participantRepository, _, _) = BuildStack();
            int hangoutId = service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, MakeDoctor(1));

            service.JoinHangout(hangoutId, MakeDoctor(2, "Joe", "Smith"));

            Assert.Equal(2, participantRepository.GetAllParticipants().Count);
        }

        [Fact]
        public void JoinHangout_WhenStaffAlreadyJoined_ThrowsInvalidOperationException()
        {
            var (service, _, _, _, _) = BuildStack();
            int hangoutId = service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, MakeDoctor(1));

            Assert.Throws<InvalidOperationException>(() =>
                service.JoinHangout(hangoutId, MakeDoctor(1)));
        }

        [Fact]
        public void GetAllHangouts_WhenStaffRosterAvailable_HydratesParticipantList()
        {
            var (service, _, _, _, staffRepository) = BuildStack();
            var creator = MakeDoctor(1);
            staffRepository.Members.Add(creator);
            service.CreateHangout("Team lunch", "desc", FutureHangoutDate, 5, creator);

            var loaded = service.GetAllHangouts().Single();

            Assert.Equal(1, loaded.ParticipantList.Single().StaffID);
        }
    }
}
