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
            service = new HangoutService(hangoutRepository.Object);
            creator = BuildDoctor(1);
        }

        [Fact]
        public void CreateHangout_WhenTitleIsNull_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);

            var ex = Assert.Throws<ArgumentException>(
                () => service.CreateHangout(null!, "desc", date, 5, creator));

            Assert.Contains("Title must be between", ex.Message);
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

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionExceedsMax_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(10);
            var description = new string('x', 101);

            var ex = Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", description, date, 5, creator));

            Assert.Contains("Description must be at most", ex.Message);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionIsAtMax_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(10);
            var description = new string('x', 100);

            service.CreateHangout("Valid title", description, date, 5, creator);

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDescriptionIsNull_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(10);

            service.CreateHangout("Valid title", null!, date, 5, creator);

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDateIsInThePast_ThrowsArgumentException()
        {
            var date = DateTime.Now.AddDays(-1);

            var ex = Assert.Throws<ArgumentException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            Assert.Contains("at least 1 week away", ex.Message);
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

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenDateIsFarInTheFuture_PersistsHangout()
        {
            var date = DateTime.Now.AddDays(30);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasConflict_ThrowsInvalidOperationException()
        {
            var date = DateTime.Now.AddDays(10);
            hangoutRepository.Setup(r => r.HasConflictsOnDate(creator.StaffID, date)).Returns(true);

            var ex = Assert.Throws<InvalidOperationException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            Assert.Contains("active scheduled appointments", ex.Message);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasConflict_DoesNotCallAddHangout()
        {
            var date = DateTime.Now.AddDays(10);
            hangoutRepository.Setup(r => r.HasConflictsOnDate(creator.StaffID, date)).Returns(true);

            Assert.Throws<InvalidOperationException>(
                () => service.CreateHangout("Valid title", "desc", date, 5, creator));

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Never);
        }

        [Fact]
        public void CreateHangout_WhenValid_CallsAddHangoutOnce()
        {
            var date = DateTime.Now.AddDays(10);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            hangoutRepository.Verify(r => r.AddHangout(It.IsAny<Hangout>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_WhenValid_AddsCreatorAsParticipant()
        {
            var date = DateTime.Now.AddDays(10);
            Hangout? captured = null;
            hangoutRepository.Setup(r => r.AddHangout(It.IsAny<Hangout>()))
                .Callback<Hangout>(h => captured = h);

            service.CreateHangout("Valid title", "desc", date, 5, creator);

            Assert.NotNull(captured);
            Assert.Single(captured!.ParticipantList);
            Assert.Equal(creator.StaffID, captured.ParticipantList[0].StaffID);
        }

        [Fact]
        public void JoinHangout_WhenHangoutNotFound_ThrowsArgumentException()
        {
            hangoutRepository.Setup(r => r.GetHangoutById(99)).Returns((Hangout?)null);
            var staff = BuildDoctor(2);

            var ex = Assert.Throws<ArgumentException>(() => service.JoinHangout(99, staff));

            Assert.Equal("Hangout not found.", ex.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutIsFull_ThrowsInvalidOperationException()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 2);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangout.ParticipantList.Add(BuildDoctor(11));
            hangoutRepository.Setup(r => r.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            var ex = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Equal("This hangout is already full.", ex.Message);
        }

        [Fact]
        public void JoinHangout_WhenHangoutIsFull_DoesNotCallAddParticipant()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 1);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(r => r.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            hangoutRepository.Verify(r => r.AddParticipant(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void JoinHangout_WhenStaffAlreadyJoined_ThrowsInvalidOperationException()
        {
            var staff = BuildDoctor(2);
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangout.ParticipantList.Add(staff);
            hangoutRepository.Setup(r => r.GetHangoutById(5)).Returns(hangout);

            var ex = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Equal("You have already joined this hangout.", ex.Message);
        }

        [Fact]
        public void JoinHangout_WhenStaffHasSchedulingConflict_ThrowsInvalidOperationException()
        {
            var date = DateTime.Now.AddDays(10);
            var hangout = new Hangout(5, "Some title", "d", date, 5);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(r => r.GetHangoutById(5)).Returns(hangout);
            hangoutRepository.Setup(r => r.HasConflictsOnDate(2, date)).Returns(true);
            var staff = BuildDoctor(2);

            var ex = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(5, staff));

            Assert.Contains("active scheduled appointments", ex.Message);
        }

        [Fact]
        public void JoinHangout_WhenValid_CallsAddParticipantWithStaffId()
        {
            var hangout = new Hangout(5, "Some title", "d", DateTime.Now.AddDays(10), 5);
            hangout.ParticipantList.Add(BuildDoctor(10));
            hangoutRepository.Setup(r => r.GetHangoutById(5)).Returns(hangout);
            var staff = BuildDoctor(2);

            service.JoinHangout(5, staff);

            hangoutRepository.Verify(r => r.AddParticipant(5, 2), Times.Once);
        }

        private static Doctor BuildDoctor(int staffId)
            => new Doctor(staffId, "First", "Last", "email@example.com", string.Empty, true, "Cardiology", "LIC-1", DoctorStatus.AVAILABLE, 3);
    }
}
