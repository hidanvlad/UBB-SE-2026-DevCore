using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class HangoutServiceTests
    {
        private readonly Mock<IHangoutRepository> hangoutRepository = new();
        private readonly Mock<IHangoutParticipantRepository> hangoutParticipantRepository = new();
        private readonly Mock<IAppointmentRepository> appointmentRepository = new();
        private readonly Mock<IStaffRepository> staffRepository = new();
        private readonly IStaff creator;

        public HangoutServiceTests()
        {
            appointmentRepository
                .Setup(repository => repository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>());
            hangoutParticipantRepository
                .Setup(repository => repository.GetAllParticipants())
                .Returns(new List<(int HangoutId, int StaffId)>());
            staffRepository
                .Setup(repository => repository.LoadAllStaff())
                .Returns(new List<IStaff>());
            creator = BuildDoctor(1);
        }

        private HangoutService CreateService() =>
            new HangoutService(
                hangoutRepository.Object,
                hangoutParticipantRepository.Object,
                appointmentRepository.Object,
                staffRepository.Object);

        [Fact]
        public void CreateHangout_WhenTitleIsNull_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);

            var exception = Assert.Throws<ArgumentException>(
                () => CreateService().CreateHangout(null!, "desc", date, 5, creator));

            Assert.Contains("Title must be between", exception.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(26)]
        [InlineData(100)]
        public void CreateHangout_WhenTitleLengthOutsideBounds_ThrowsArgumentException(int length)
        {
            var date = DateTime.Now.AddDays(10);
            var title = new string('x', length);

            Assert.Throws<ArgumentException>(
                () => CreateService().CreateHangout(title, "desc", date, 5, creator));
        }

        [Fact]
        public void CreateHangout_WhenDescriptionExceedsMax_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);
            var description = new string('x', 101);

            var exception = Assert.Throws<ArgumentException>(
                () => CreateService().CreateHangout("Valid title", description, date, 5, creator));

            Assert.Contains("Description must be at most", exception.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        public void CreateHangout_WhenDateIsLessThanOneWeekAhead_ThrowsArgumentException(int daysAhead)
        {
            var date = DateTime.Now.AddDays(daysAhead);

            Assert.Throws<ArgumentException>(
                () => CreateService().CreateHangout("Valid title", "desc", date, 5, creator));
        }

        [Fact]
        public void CreateHangout_WhenValid_PersistsHangoutAndAddsCreatorAsParticipant()
        {
            var date = DateTime.Now.Date.AddDays(10);
            hangoutRepository
                .Setup(repository => repository.AddHangout("Valid title", "desc", date, 5))
                .Returns(42);

            int newHangoutId = CreateService().CreateHangout("Valid title", "desc", date, 5, creator);

            Assert.Equal(42, newHangoutId);
            hangoutRepository.Verify(repository => repository.AddHangout("Valid title", "desc", date, 5), Times.Once);
            hangoutParticipantRepository.Verify(repository => repository.AddParticipant(42, creator.StaffID), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasActiveAppointmentOnDate_ThrowsInvalidOperationException()
        {
            var date = DateTime.Now.Date.AddDays(10);
            appointmentRepository
                .Setup(repository => repository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>
                {
                    new Appointment { DoctorId = creator.StaffID, Date = date, Status = "Scheduled" },
                });

            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateService().CreateHangout("Valid title", "desc", date, 5, creator));

            Assert.Contains("active scheduled appointments", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutNotFound_ThrowsArgumentException()
        {
            hangoutRepository.Setup(repository => repository.GetHangoutById(99)).Returns((Hangout?)null);

            var exception = Assert.Throws<ArgumentException>(
                () => CreateService().JoinHangout(99, BuildDoctor(2)));

            Assert.Equal("Hangout not found.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutIsFull_ThrowsInvalidOperationException()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 2);
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
                .Returns(new List<(int HangoutId, int StaffId)> { (5, 10), (5, 11) });

            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateService().JoinHangout(5, BuildDoctor(2)));

            Assert.Equal("This hangout is already full.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenStaffAlreadyJoined_ThrowsInvalidOperationException()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
                .Returns(new List<(int HangoutId, int StaffId)> { (5, 2) });

            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateService().JoinHangout(5, BuildDoctor(2)));

            Assert.Equal("You have already joined this hangout.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenValid_AddsParticipant()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);

            CreateService().JoinHangout(5, BuildDoctor(2));

            hangoutParticipantRepository.Verify(repository => repository.AddParticipant(5, 2), Times.Once);
        }

        [Fact]
        public void GetAllHangouts_HydratesParticipantsFromStaffRepository()
        {
            var hangout = new Hangout(1, "Lunch", "desc", DateTime.Now.AddDays(10), 5);
            var staffMember = BuildDoctor(7);
            hangoutRepository.Setup(repository => repository.GetAllHangouts()).Returns(new List<Hangout> { hangout });
            hangoutParticipantRepository.Setup(repository => repository.GetAllParticipants())
                .Returns(new List<(int HangoutId, int StaffId)> { (1, 7) });
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { staffMember });

            var hangouts = CreateService().GetAllHangouts();

            Assert.Single(hangouts);
            Assert.Single(hangouts[0].ParticipantList);
            Assert.Equal(7, hangouts[0].ParticipantList[0].StaffID);
        }

        private static Doctor BuildDoctor(int staffId) =>
            new Doctor(staffId, "First", "Last", "email@example.com", true, "Cardiology", "LIC-1", DoctorStatus.AVAILABLE, 3);
    }
}
