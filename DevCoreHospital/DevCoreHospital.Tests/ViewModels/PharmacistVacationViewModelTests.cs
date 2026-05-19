using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Pharmacy;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class PharmacistVacationViewModelTests
    {
        private readonly Mock<IPharmacyVacationService> mockService;
        private readonly PharmacistVacationViewModel viewModel;

        private static readonly Pharmacyst TestPharmacist =
            new Pharmacyst(1, "Ana", "Pop", string.Empty, true, "General", 3);

        private static readonly PharmacistVacationViewModel.PharmacistChoice TestChoice =
            new(TestPharmacist, "Ana Pop");

        private static readonly DateTimeOffset StartDate =
            new DateTimeOffset(new DateTime(2025, 7, 1));

        private static readonly DateTimeOffset EndDate =
            new DateTimeOffset(new DateTime(2025, 7, 3));

        public PharmacistVacationViewModelTests()
        {
            mockService = new Mock<IPharmacyVacationService>();
            mockService.Setup(vacationService => vacationService.GetPharmacists()).Returns(new List<Pharmacyst>());
            viewModel = new PharmacistVacationViewModel(mockService.Object);
        }


        [Fact]
        public void TryRegisterVacation_ReturnsWarningStatus_WhenPharmacistIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(null, StartDate, EndDate);

            Assert.Equal(VacationRegistrationStatus.Warning, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsSelectPharmacistMessage_WhenPharmacistIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(null, StartDate, EndDate);

            Assert.Equal("Select a pharmacist first.", vacation.message);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsWarningStatus_WhenStartDateIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, null, EndDate);

            Assert.Equal(VacationRegistrationStatus.Warning, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsWarningStatus_WhenEndDateIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, null);

            Assert.Equal(VacationRegistrationStatus.Warning, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsSelectDatesMessage_WhenStartDateIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, null, EndDate);

            Assert.Equal("Select both start and end dates.", vacation.message);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsSelectDatesMessage_WhenEndDateIsNull()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, null);

            Assert.Equal("Select both start and end dates.", vacation.message);
        }


        [Fact]
        public void TryRegisterVacation_ReturnsSuccessStatus_WhenServiceCallSucceeds()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal(VacationRegistrationStatus.Success, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsSuccessMessage_WhenServiceCallSucceeds()
        {
            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal("Vacation shift added to repository.", vacation.message);
        }

        [Fact]
        public void TryRegisterVacation_CallsServiceWithDateComponentOnly_WhenDatesHaveTimeComponent()
        {
            var startWithTime = new DateTimeOffset(new DateTime(2025, 7, 1, 14, 30, 0));
            var endWithTime = new DateTimeOffset(new DateTime(2025, 7, 3, 22, 0, 0));

            viewModel.TryRegisterVacation(TestChoice, startWithTime, endWithTime);

            mockService.Verify(vacationService => vacationService.RegisterVacation(
                TestPharmacist.StaffID,
                startWithTime.Date,
                endWithTime.Date), Times.Once);
        }


        [Fact]
        public void TryRegisterVacation_ReturnsErrorStatus_WhenServiceThrowsArgumentException()
        {
            mockService
                .Setup(vacationService => vacationService.RegisterVacation(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Throws(new ArgumentException("End date must be on or after start date."));

            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal(VacationRegistrationStatus.Error, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsExceptionMessage_WhenServiceThrowsArgumentException()
        {
            const string exceptionMessage = "End date must be on or after start date.";
            mockService
                .Setup(vacationService => vacationService.RegisterVacation(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Throws(new ArgumentException(exceptionMessage));

            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal(exceptionMessage, vacation.message);
        }


        [Fact]
        public void TryRegisterVacation_ReturnsErrorStatus_WhenServiceThrowsInvalidOperationException()
        {
            mockService
                .Setup(vacationService => vacationService.RegisterVacation(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Throws(new InvalidOperationException("Cannot add vacation: this period overlaps an existing shift."));

            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal(VacationRegistrationStatus.Error, vacation.status);
        }

        [Fact]
        public void TryRegisterVacation_ReturnsExceptionMessage_WhenServiceThrowsInvalidOperationException()
        {
            const string exceptionMessage = "Cannot add vacation: this period overlaps an existing shift.";
            mockService
                .Setup(vacationService => vacationService.RegisterVacation(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Throws(new InvalidOperationException(exceptionMessage));

            var vacation = viewModel.TryRegisterVacation(TestChoice, StartDate, EndDate);

            Assert.Equal(exceptionMessage, vacation.message);
        }


        [Fact]
        public void LoadPharmacists_ResultsInEmptyCollection_WhenServiceReturnsNoPharmacists()
        {
            mockService.Setup(vacationService => vacationService.GetPharmacists()).Returns(new List<Pharmacyst>());

            viewModel.LoadPharmacists();

            Assert.Empty(viewModel.Pharmacists);
        }

        [Fact]
        public void LoadPharmacists_AddsPharmacistToCollection_WhenServiceReturnsOnePharmacist()
        {
            mockService.Setup(vacationService => vacationService.GetPharmacists()).Returns(new List<Pharmacyst> { TestPharmacist });

            viewModel.LoadPharmacists();

            Assert.Single(viewModel.Pharmacists);
        }

        [Fact]
        public void LoadPharmacists_BuildsDisplayName_FromFirstAndLastName()
        {
            mockService.Setup(vacationService => vacationService.GetPharmacists()).Returns(new List<Pharmacyst> { TestPharmacist });

            viewModel.LoadPharmacists();

            Assert.Equal("Ana Pop", viewModel.Pharmacists[0].displayName);
        }

        [Fact]
        public void LoadPharmacists_ExcludesWhitespaceParts_WhenLastNameIsWhitespace()
        {
            var pharmacistWithBlankLastName = new Pharmacyst(2, "Ion", "   ", string.Empty, true, "General", 1);
            mockService.Setup(vacationService => vacationService.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacistWithBlankLastName });

            viewModel.LoadPharmacists();

            Assert.Equal("Ion", viewModel.Pharmacists[0].displayName);
        }

        [Fact]
        public void LoadPharmacists_ClearsPreviousCollection_WhenCalledASecondTime()
        {
            mockService.SetupSequence(vacationService => vacationService.GetPharmacists())
                .Returns(new List<Pharmacyst> { TestPharmacist })
                .Returns(new List<Pharmacyst>());

            viewModel.LoadPharmacists();

            viewModel.LoadPharmacists();

            Assert.Empty(viewModel.Pharmacists);
        }
    }
}
