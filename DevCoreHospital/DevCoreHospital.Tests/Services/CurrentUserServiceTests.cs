using DevCoreHospital.Models;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Services
{
    public class CurrentUserServiceTests
    {
        private readonly CurrentUserService service;

        public CurrentUserServiceTests()
        {
            service = new CurrentUserService();
            service.RoleType = UserRole.Doctor;
        }

        [Fact]
        public void UserId_IsOne_ByDefault()
        {
            Assert.Equal(1, service.UserId);
        }

        [Fact]
        public void RoleType_IsDoctor_ByDefault()
        {
            Assert.Equal(UserRole.Doctor, service.RoleType);
        }

        [Fact]
        public void Role_ReturnsDoctorString_ByDefault()
        {
            Assert.Equal("Doctor", service.Role);
        }

        [Fact]
        public void RoleType_CanBeChangedToPharmacist()
        {
            service.RoleType = UserRole.Pharmacist;

            Assert.Equal(UserRole.Pharmacist, service.RoleType);
        }

        [Fact]
        public void Role_ReturnsPharmacistString_WhenRoleTypeIsPharmacist()
        {
            service.RoleType = UserRole.Pharmacist;

            Assert.Equal("Pharmacist", service.Role);
        }

        [Fact]
        public void RoleType_CanBeChangedToAdmin()
        {
            service.RoleType = UserRole.Admin;

            Assert.Equal(UserRole.Admin, service.RoleType);
        }

        [Fact]
        public void Role_ReturnsAdminString_WhenRoleTypeIsAdmin()
        {
            service.RoleType = UserRole.Admin;

            Assert.Equal("Admin", service.Role);
        }

        [Fact]
        public void Role_ReflectsLatestRoleType_AfterMultipleChanges()
        {
            service.RoleType = UserRole.Pharmacist;
            service.RoleType = UserRole.Doctor;

            Assert.Equal("Doctor", service.Role);
        }
    }
}
