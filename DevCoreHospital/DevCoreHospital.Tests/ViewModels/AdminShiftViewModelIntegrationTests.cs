using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Repositories;
using DevCoreHospital.ViewModels.Admin;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DevCoreHospital.Tests.ViewModels
{
    public class AdminShiftViewModelIntegrationTests : IClassFixture<SqlTestFixture>
    {
        private readonly SqlTestFixture database;

        public AdminShiftViewModelIntegrationTests(SqlTestFixture database) => this.database = database;

        [Fact]
        public void CreateNewShift_WhenNoOverlap_AddsShiftToRepositoryAndViewModel()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "Create", "ShiftVmTest", "Cardiology");
            try
            {
                var staffRepository = new StaffRepository(database.ConnectionString);
                var shiftRepository = new ShiftRepository(database.ConnectionString, staffRepository);
                var service = new ShiftManagementService(staffRepository, shiftRepository);
                var viewModel = new AdminShiftViewModel(service);
                var staff = staffRepository.GetStaffById(staffId)!;
                var start = DateTime.Today.AddHours(8);
                var end = DateTime.Today.AddHours(12);
                var initialCountForStaff = shiftRepository.GetShiftsByStaffID(staffId).Count;

                viewModel.CreateNewShift(staff, start, end, "ER");

                Assert.Equal(initialCountForStaff + 1, shiftRepository.GetShiftsByStaffID(staffId).Count);
                bool IsShiftForStaff(Shift shift) => shift.AppointedStaff.StaffID == staffId;
                var staffShift = Assert.Single(shiftRepository.GetShifts().Where(IsShiftForStaff));
                Assert.Equal("ER", staffShift.Location);
                Assert.Equal(ShiftStatus.SCHEDULED, staffShift.Status);
                bool IsViewModelShiftForStaff(Shift shift) => shift.AppointedStaff.StaffID == staffId && shift.Location == "ER";
                Assert.Contains(viewModel.Shifts, IsViewModelShiftForStaff);
            }
            finally
            {
                DeleteShiftsByStaff(connection, staffId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStatusInRepositoryAndViewModel()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "SetActiveVm", "ShiftVmTest", "Neurology");
            var start = DateTime.Today.AddHours(8);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(8), "SCHEDULED");
            try
            {
                var staffRepository = new StaffRepository(database.ConnectionString);
                var shiftRepository = new ShiftRepository(database.ConnectionString, staffRepository);
                var service = new ShiftManagementService(staffRepository, shiftRepository);
                var viewModel = new AdminShiftViewModel(service);

                viewModel.SetShiftActive(shiftId);

                bool IsShiftById(Shift shift) => shift.Id == shiftId;
                var repoShift = Assert.Single(shiftRepository.GetShifts().Where(IsShiftById));
                Assert.Equal(ShiftStatus.ACTIVE, repoShift.Status);
                bool IsViewModelShiftById(Shift shift) => shift.Id == shiftId;
                var vmShift = Assert.Single(viewModel.Shifts.Where(IsViewModelShiftById));
                Assert.Equal(ShiftStatus.ACTIVE, vmShift.Status);
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStatusInRepositoryAndViewModel()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "CancelVm", "ShiftVmTest", "Oncology");
            var start = DateTime.Today.AddHours(9);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(8), "SCHEDULED");
            try
            {
                var staffRepository = new StaffRepository(database.ConnectionString);
                var shiftRepository = new ShiftRepository(database.ConnectionString, staffRepository);
                var service = new ShiftManagementService(staffRepository, shiftRepository);
                var viewModel = new AdminShiftViewModel(service);

                viewModel.CancelShift(shiftId);

                bool IsShiftById(Shift shift) => shift.Id == shiftId;
                var repoShift = Assert.Single(shiftRepository.GetShifts().Where(IsShiftById));
                Assert.Equal(ShiftStatus.COMPLETED, repoShift.Status);
                bool IsViewModelShiftById(Shift shift) => shift.Id == shiftId;
                var vmShift = Assert.Single(viewModel.Shifts.Where(IsViewModelShiftById));
                Assert.Equal(ShiftStatus.COMPLETED, vmShift.Status);
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void SelectedDepartment_WhenSet_FiltersShiftsInViewModel()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "FilterVm", "ShiftVmTest", "Cardiology");
            // Use tomorrow so the shifts fall within the current week regardless of today's day-of-week
            var tomorrow = DateTime.Today.AddDays(1);
            var erShiftId = database.InsertShift(connection, staffId, "ER", tomorrow.AddHours(8), tomorrow.AddHours(10));
            var pharmacyShiftId = database.InsertShift(connection, staffId, "Pharmacy", tomorrow.AddHours(11), tomorrow.AddHours(13));
            try
            {
                var staffRepository = new StaffRepository(database.ConnectionString);
                var shiftRepository = new ShiftRepository(database.ConnectionString, staffRepository);
                var service = new ShiftManagementService(staffRepository, shiftRepository);
                var viewModel = new AdminShiftViewModel(service);

                viewModel.IsWeeklyView = true;
                viewModel.SelectedDepartment = "ER";

                bool IsErShift(Shift shift) => shift.Id == erShiftId && shift.Location == "ER";
                Assert.Contains(viewModel.Shifts, IsErShift);
                bool IsPharmacyShift(Shift shift) => shift.Id == pharmacyShiftId;
                Assert.DoesNotContain(viewModel.Shifts, IsPharmacyShift);
            }
            finally
            {
                database.DeleteShift(connection, erShiftId);
                database.DeleteShift(connection, pharmacyShiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        private static void DeleteShiftsByStaff(SqlConnection connection, int staffId)
        {
            using var command = new SqlCommand("DELETE FROM Shifts WHERE staff_id = @Id", connection);
            command.Parameters.AddWithValue("@Id", staffId);
            command.ExecuteNonQuery();
        }
    }
}
