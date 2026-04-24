using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class HangoutServiceTests
    {
        private readonly Mock<IHangoutRepository> hangoutRepository;
        private readonly HangoutService service;
        private readonly IStaff creator;

        public HangoutServiceTests()
        {
            hangoutRepository = new Mock<IHangoutRepository>();
            hangoutRepository
                .Setup(repository => repository.GetAppointmentStatusesForStaffOnDate(It.IsAny<int>(), It.IsAny<DateTime>()))
                .Returns(new List<string>());
            service = new HangoutService(hangoutRepository.Object);
            creator = BuildDoctor(1);
        }

        [Fact]
        public void CreateHangout_WhenTitleIsNull_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);

            var exception = Assert.Throws<ArgumentException>(
                () => service.CreateHangout(null!, "desc", date, 5, creator));

            Assert.Contains("Title must be between", exception.Message);
        }

        [Fact]
        public void CreateHangout_WhenTitleIsEmpty_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout(string.Empty, "desc", date, 5, creator));
        }

        [Fact]
        public void CreateHangout_WhenTitleIsWhitespace_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout("   ", "desc", date, 5, creator));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void CreateHangout_WhenTitleIsShorterThanMin_ThrowsArgumentException(int length)
        {
            var date = DateTime.Now.AddDays(10);
            var title = new string('x', length);

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout(title, "desc", date, 5, creator));
        }

        [Theory]
        [InlineData(26)]
        [InlineData(100)]
        public void CreateHangout_WhenTitleIsLongerThanMax_ThrowsArgumentException(int length)
        {
            var date = DateTime.Now.AddDays(10);
            var title = new string('x', length);

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout(title, "desc", date, 5, creator));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(25)]
        public void CreateHangout_WhenTitleIsOnBoundary_PersistsHangout(int length)
        {
            var date = DateTime.Now.AddDays(10);
            var title = new string('x', length);

            service.CreateHangout(title, "desc", date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionExceedsMax_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);
            var description = new string('x', 101);

            var exception = Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", description, date, 5, creator));

            Assert.Contains("Description must be at most", exception.Message);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionIsAtMax_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(10);
            var description = new string('x', 100);

            service.CreateHangout("Valid title", description, date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionIsNull_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(10);

            service.CreateHangout("Valid title", null!, date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDateIsInThePast_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(-1);

            var exception = Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            Assert.Contains("at least 1 week away", exception.Message);
        }

        [Fact]
        public void CreateHangout_WhenDateIsToday_ThrowsArgumentException()
        {
            var date = DateTime.Now;

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        public void CreateHangout_WhenDateIsLessThanOneWeekAhead_ThrowsArgumentException(int daysAhead)
        {
            var date = DateTime.Now.AddDays(daysAhead);

            Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));
        }

        [Fact]
        public void CreateHangout_WhenDateIsExactlyOneWeekAhead_PersistsHangout()
        {
            var date = DateTime.Now.Date.AddDays(7);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDateIsFarInTheFuture_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(30);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasConflict_ThrowsInvalidOperationException()
        {
            var date = DateTime.Now.AddDays(10);
            hangoutRepository
                .Setup(repository => repository.GetAppointmentStatusesForStaffOnDate(creator.StaffID, date))
                .Returns(new List<string> { "Scheduled" });

            var exception = Assert.Throws<InvalidOperationException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            Assert.Contains("active scheduled appointments", exception.Message);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasConflict_DoesNotCallAddHangout()
        {
            var date = DateTime.Now.AddDays(10);
            hangoutRepository
                .Setup(repository => repository.GetAppointmentStatusesForStaffOnDate(creator.StaffID, date))
                .Returns(new List<string> { "Scheduled" });

            Assert.Throws<InvalidOperationException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Never);
        }

        [Fact]
        public void CreateHangout_WhenValid_CallsAddHangoutOnce()
        {
            var date = DateTime.Now.AddDays(10);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            hangoutRepository.Verify(repository => repository.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenValid_AddsCreatorAsParticipant()
        {
            var date = DateTime.Now.AddDays(10);
            Hangout? captured = null;

            void CaptureHangout(Hangout hangout) { captured = hangout; }

            hangoutRepository.Setup(repository => repository.AddHangout(It.IsAny<Hangout>()))
                .Callback<Hangout>(CaptureHangout);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            Assert.NotNull(captured);
            Assert.Single(captured!.ParticipantList);
            Assert.Equal(creator.StaffID, captured.ParticipantList[0].StaffID);
        }

        [Fact]
        public void JoinHangout_WhenHangoutNotFound_ThrowsArgumentException()
        {
            hangoutRepository.Setup(repository => repository.GetHangoutById(99)).Returns((Hangout?)null);
            var staff = BuildDoctor(2);

            var exception = Assert.Throws<ArgumentException>(() => service.JoinHangout(99, staff));

            Assert.Equal("Hangout not found.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutIsFull_ThrowsInvalidOperationException()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 2);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangout.ParticipantList.Add(BuildDoctor(11));
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            var exception = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Equal("This hangout is already full.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutIsFull_DoesNotCallAddParticipant()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 1);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            hangoutRepository.Verify(repository => repository.AddParticipant(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void JoinHangout_WhenStaffAlreadyJoined_ThrowsInvalidOperationException()
        {
            var staff = BuildDoctor(2);
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangout.ParticipantList.Add(staff);
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);

            var exception = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Equal("You have already joined this hangout.", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenStaffHasSchedulingConflict_ThrowsInvalidOperationException()
        {
            var date = DateTime.Now.AddDays(10);
            var hangout = new Hangout(5, "Some title", "d", date, 5);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            hangoutRepository
                .Setup(repository => repository.GetAppointmentStatusesForStaffOnDate(2, date))
                .Returns(new List<string> { "Scheduled" });
            var staff = BuildDoctor(2);

            var exception = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Contains("active scheduled appointments", exception.Message);
        }

        [Fact]
        public void JoinHangout_WhenValid_CallsAddParticipantWithStaffId()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(repository => repository.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            service.JoinHangout(5, staff);

            hangoutRepository.Verify(repository => repository.AddParticipant(5, 2), Times.Once);
        }

        [Fact]
        public void GetAllHangouts_ReturnsHangoutsFromRepository()
        {
            var expected = new List<Hangout>
            {
                new Hangout(1, "Team Lunch", "desc", DateTime.Now.AddDays(10), 5),
                new Hangout(2, "Coffee Chat", "desc2", DateTime.Now.AddDays(14), 3),
            };
            hangoutRepository.Setup(repository => repository.GetAllHangouts()).Returns(expected);

            var result = service.GetAllHangouts();

            Assert.Same(expected, result);
            hangoutRepository.Verify(repository => repository.GetAllHangouts(), Times.Once);
        }

        [Fact]
        public void GetAllHangouts_WhenRepositoryReturnsEmpty_ReturnsEmptyList()
        {
            hangoutRepository.Setup(repository => repository.GetAllHangouts()).Returns(new List<Hangout>());

            var result = service.GetAllHangouts();

            Assert.Empty(result);
        }

        private static Doctor BuildDoctor(int staffId)
            => new Doctor(staffId, "First", "Last", "email@example.com", string.Empty, true, "Cardiology", "LIC-1", DoctorStatus.AVAILABLE, 3);
    }
}
