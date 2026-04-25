using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class HangoutViewModelTests
    {
        private readonly Mock<IHangoutService> mockService;
        private readonly HangoutViewModel viewModel;

        private static readonly DoctorScheduleViewModel.DoctorOption TestDoctor = new DoctorScheduleViewModel.DoctorOption
        {
            DoctorId = 1,
            DoctorName = "Ana Pop",
            FirstName = "Ana",
            LastName = "Pop"
        };

        public HangoutViewModelTests()
        {
            mockService = new Mock<IHangoutService>();
            mockService.Setup(hangoutService => hangoutService.GetAllHangouts()).Returns(new List<Hangout>());
            viewModel = new HangoutViewModel(mockService.Object);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void CreateCommand_CannotExecute_WhenTitleLengthIsBelowMinimum(int length)
        {
            viewModel.Title = new string('x', length);
            viewModel.SelectedDoctor = TestDoctor;

            Assert.False(viewModel.CreateCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(26)]
        [InlineData(50)]
        public void CreateCommand_CannotExecute_WhenTitleLengthExceedsMaximum(int length)
        {
            viewModel.Title = new string('x', length);
            viewModel.SelectedDoctor = TestDoctor;

            Assert.False(viewModel.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CreateCommand_CannotExecute_WhenDescriptionExceedsMaximumLength()
        {
            viewModel.Title = "Valid Title";
            viewModel.Description = new string('x', 101);
            viewModel.SelectedDoctor = TestDoctor;

            Assert.False(viewModel.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CreateCommand_CannotExecute_WhenSelectedDoctorIsNull()
        {
            viewModel.Title = "Valid Title";
            viewModel.SelectedDoctor = null;

            Assert.False(viewModel.CreateCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(25)]
        public void CreateCommand_CanExecute_WhenTitleIsOnBoundary(int length)
        {
            viewModel.Title = new string('x', length);
            viewModel.SelectedDoctor = TestDoctor;

            Assert.True(viewModel.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CreateCommand_CanExecute_WhenAllFieldsAreValid()
        {
            viewModel.Title = "Valid Title";
            viewModel.Description = "A description";
            viewModel.SelectedDoctor = TestDoctor;

            Assert.True(viewModel.CreateCommand.CanExecute(null));
        }

        [Fact]
        public void CreateHangout_CallsServiceWithCorrectTitle()
        {
            viewModel.Title = "My Hangout";
            viewModel.Description = "desc";
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.CreateCommand.Execute(null);

            mockService.Verify(hangoutService => hangoutService.CreateHangout(
                "My Hangout",
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<IStaff>()), Times.Once);
        }

        [Fact]
        public void CreateHangout_SetsSuccessMessage_WhenServiceSucceeds()
        {
            viewModel.Title = "My Hangout";
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.CreateCommand.Execute(null);

            Assert.Equal("Hangout created successfully!", viewModel.SuccessMessage);
        }

        [Fact]
        public void CreateHangout_ClearsTitle_AfterSuccess()
        {
            viewModel.Title = "My Hangout";
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.CreateCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.Title);
        }

        [Fact]
        public void CreateHangout_ClearsDescription_AfterSuccess()
        {
            viewModel.Title = "My Hangout";
            viewModel.Description = "Some description";
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.CreateCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.Description);
        }

        [Fact]
        public void CreateHangout_SetsErrorMessage_WhenServiceThrows()
        {
            mockService
                .Setup(hangoutService => hangoutService.CreateHangout(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                    It.IsAny<int>(), It.IsAny<IStaff>()))
                .Throws(new InvalidOperationException("Service error"));

            viewModel.Title = "My Hangout";
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.CreateCommand.Execute(null);

            Assert.Equal("Service error", viewModel.ErrorMessage);
        }

        [Fact]
        public void JoinHangoutById_SetsErrorMessage_WhenSelectedDoctorIsNull()
        {
            viewModel.SelectedDoctor = null;

            viewModel.JoinHangoutById(1);

            Assert.Equal("Please select a doctor to join the hangout.", viewModel.ErrorMessage);
        }

        [Fact]
        public void JoinHangoutById_DoesNotCallService_WhenSelectedDoctorIsNull()
        {
            viewModel.SelectedDoctor = null;

            viewModel.JoinHangoutById(1);

            mockService.Verify(hangoutService => hangoutService.JoinHangout(It.IsAny<int>(), It.IsAny<IStaff>()), Times.Never);
        }

        [Fact]
        public void JoinHangoutById_SetsSuccessMessage_WhenServiceSucceeds()
        {
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.JoinHangoutById(1);

            Assert.Equal("Joined hangout successfully!", viewModel.SuccessMessage);
        }

        [Fact]
        public void JoinHangoutById_CallsServiceWithCorrectHangoutId()
        {
            viewModel.SelectedDoctor = TestDoctor;

            viewModel.JoinHangoutById(42);

            mockService.Verify(hangoutService => hangoutService.JoinHangout(42, It.IsAny<IStaff>()), Times.Once);
        }

        [Fact]
        public void JoinHangoutById_SetsErrorMessage_WhenServiceThrows()
        {
            mockService
                .Setup(hangoutService => hangoutService.JoinHangout(It.IsAny<int>(), It.IsAny<IStaff>()))
                .Throws(new InvalidOperationException("Already joined."));

            viewModel.SelectedDoctor = TestDoctor;

            viewModel.JoinHangoutById(1);

            Assert.Equal("Already joined.", viewModel.ErrorMessage);
        }

        [Fact]
        public void Hangouts_IsEmpty_WhenServiceReturnsNoHangouts()
        {
            mockService.Setup(hangoutService => hangoutService.GetAllHangouts()).Returns(new List<Hangout>());

            var localViewModel = new HangoutViewModel(mockService.Object);

            Assert.Empty(localViewModel.Hangouts);
        }

        [Fact]
        public void Hangouts_ContainsOneItem_WhenServiceReturnsOneHangout()
        {
            var hangout = new Hangout(1, "Team Lunch", "desc", DateTime.Now.AddDays(10), 5);
            mockService.Setup(hangoutService => hangoutService.GetAllHangouts()).Returns(new List<Hangout> { hangout });

            var localViewModel = new HangoutViewModel(mockService.Object);

            Assert.Single(localViewModel.Hangouts);
        }

        [Fact]
        public void MaxParticipants_DefaultIsFive()
        {
            Assert.Equal(5, viewModel.MaximumParticipants);
        }

        [Fact]
        public void MaxParticipantsOptions_ContainsExpectedValues()
        {
            Assert.Equal(new[] { 2, 3, 4, 5, 10, 15, 20 }, viewModel.MaximumParticipantsOptions);
        }

        [Fact]
        public void CreateHangout_ReloadsHangouts_AfterSuccess()
        {
            var hangout = new Hangout(1, "My Hangout", "desc", DateTime.Now.AddDays(10), 5);
            int getHangoutsCalls = 0;
            List<Hangout> GetHangoutsWithCount()
            {
                getHangoutsCalls++;
                return getHangoutsCalls > 1 ? new List<Hangout> { hangout } : new List<Hangout>();
            }
            mockService.Setup(hangoutService => hangoutService.GetAllHangouts()).Returns(GetHangoutsWithCount);

            var localViewModel = new HangoutViewModel(mockService.Object);
            localViewModel.Title = "My Hangout";
            localViewModel.SelectedDoctor = TestDoctor;

            localViewModel.CreateCommand.Execute(null);

            Assert.Single(localViewModel.Hangouts);
        }

        [Fact]
        public void JoinHangoutById_ReloadsHangouts_AfterSuccess()
        {
            var hangout = new Hangout(1, "Team Lunch", "desc", DateTime.Now.AddDays(10), 5);
            int getHangoutsCalls = 0;
            List<Hangout> GetHangoutsWithCount()
            {
                getHangoutsCalls++;
                return getHangoutsCalls > 1 ? new List<Hangout> { hangout } : new List<Hangout>();
            }
            mockService.Setup(hangoutService => hangoutService.GetAllHangouts()).Returns(GetHangoutsWithCount);

            var localViewModel = new HangoutViewModel(mockService.Object);
            localViewModel.SelectedDoctor = TestDoctor;

            localViewModel.JoinHangoutById(1);

            Assert.Single(localViewModel.Hangouts);
        }
    }
}
