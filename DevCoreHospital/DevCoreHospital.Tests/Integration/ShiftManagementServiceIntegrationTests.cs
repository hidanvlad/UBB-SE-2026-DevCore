using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Repositories;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DevCoreHospital.Tests.Integration
{
    public class ShiftManagementServiceIntegrationTests : IClassFixture<SqlTestFixture>
    {
        private readonly SqlTestFixture database;

        public ShiftManagementServiceIntegrationTests(SqlTestFixture database) => this.database = database;

        [Fact]
        public void AddShift_WhenShiftIsProvided_AddsShiftToDatabase()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "Add", "ShiftTest", "Cardiology");
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);
                var staff = staffRepo.GetStaffById(staffId)!;
                var start = DateTime.Today.AddDays(30).AddHours(8);
                var shift = new Shift(0, staff, "ER", start, start.AddHours(4), ShiftStatus.SCHEDULED);
                var initialCountForStaff = shiftRepo.GetShiftsByStaffID(staffId).Count;

                service.AddShift(shift);

                Assert.Equal(initialCountForStaff + 1, shiftRepo.GetShiftsByStaffID(staffId).Count);
                bool IsForStaff(Shift shiftItem) => shiftItem.AppointedStaff.StaffID == staffId;
                Assert.Contains(shiftRepo.GetShiftsByStaffID(staffId), IsForStaff);
            }
            finally
            {
                DeleteShiftsByStaff(connection, staffId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftOverlapsExistingShift_ReturnsFalse()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "Overlap", "DoctorTest", "Neurology");
            var start = DateTime.Today.AddDays(31).AddHours(8);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(4));
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);

                var result = service.ValidateNoOverlap(staffId, start.AddHours(2), start.AddHours(6));

                Assert.False(result);
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftDoesNotOverlapExistingShift_ReturnsTrue()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "NoOverlap", "DoctorTest", "Oncology");
            var start = DateTime.Today.AddDays(32).AddHours(8);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(4));
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);

                var result = service.ValidateNoOverlap(staffId, start.AddHours(4), start.AddHours(8));

                Assert.True(result);
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStatusToActiveInRepositoryAndDatabase()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "SetActive", "DoctorTest", "Cardiology");
            var start = DateTime.Today.AddDays(33).AddHours(9);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(8));
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);

                service.SetShiftActive(shiftId);

                bool HasMatchingId(Shift shiftItem) => shiftItem.Id == shiftId;
                var shift = Assert.Single(shiftRepo.GetShifts().Where(HasMatchingId));
                Assert.Equal(ShiftStatus.ACTIVE, shift.Status);
                Assert.Equal("ACTIVE", database.GetShiftStatus(connection, shiftId));
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStatusToCompletedInRepositoryAndDatabase()
        {
            using var connection = database.OpenConnection();
            var staffId = database.InsertStaff(connection, "Doctor", "Cancel", "DoctorTest", "Emergency Medicine");
            var start = DateTime.Today.AddDays(34).AddHours(7);
            var shiftId = database.InsertShift(connection, staffId, "ER", start, start.AddHours(8));
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);

                service.CancelShift(shiftId);

                bool HasMatchingId(Shift shiftItem) => shiftItem.Id == shiftId;
                var shift = Assert.Single(shiftRepo.GetShifts().Where(HasMatchingId));
                Assert.Equal(ShiftStatus.COMPLETED, shift.Status);
                Assert.Equal("COMPLETED", database.GetShiftStatus(connection, shiftId));
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, staffId);
            }
        }

        [Fact]
        public void ReassignShift_WhenInputsAreValid_ChangesAppointedStaffAndReturnsTrue()
        {
            using var connection = database.OpenConnection();
            var originalStaffId = database.InsertStaff(connection, "Doctor", "Original", "ReassignTest", "Cardiology");
            var replacementStaffId = database.InsertStaff(connection, "Doctor", "Replacement", "ReassignTest", "Cardiology");
            var start = DateTime.Today.AddDays(35).AddHours(8);
            var shiftId = database.InsertShift(connection, originalStaffId, "ER", start, start.AddHours(4));
            try
            {
                var staffRepo = new StaffRepository(database.ConnectionString);
                var shiftRepo = new ShiftRepository(database.ConnectionString);
                var service = new ShiftManagementService(staffRepo, shiftRepo);
                var shift = shiftRepo.GetShiftById(shiftId)!;
                var replacement = staffRepo.GetStaffById(replacementStaffId)!;

                var result = service.ReassignShift(shift, replacement);

                Assert.True(result);
                Assert.Equal(replacementStaffId, shift.AppointedStaff.StaffID);
            }
            finally
            {
                database.DeleteShift(connection, shiftId);
                database.DeleteStaff(connection, originalStaffId);
                database.DeleteStaff(connection, replacementStaffId);
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
