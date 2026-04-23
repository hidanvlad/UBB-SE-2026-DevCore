using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Fakes;

namespace DevCoreHospital.Tests.Integration
{
    public class HangoutFlowIntegrationTests
    {
        [Fact]
        public void CreateThenJoin_WhenNoConflicts_StoresHangoutWithBothParticipants()
        {
            var fake = new InMemoryHangoutRepository();
            var service = new HangoutService(fake);
            var creator = BuildDoctor(1);
            var joiner = BuildDoctor(2);
            var date = DateTime.Now.AddDays(10);

            service.CreateHangout("Team hangout", "Monthly meetup", date, 5, creator);
            service.JoinHangout(1, joiner);

            var hangout = fake.GetHangoutById(1);
            Assert.NotNull(hangout);
            Assert.Equal(2, hangout!.ParticipantList.Count);
            Assert.Contains(hangout.ParticipantList, p => p.StaffID == creator.StaffID);
            Assert.Contains(hangout.ParticipantList, p => p.StaffID == joiner.StaffID);
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasConflictingAppointment_ThrowsAndPersistsNothing()
        {
            var fake = new InMemoryHangoutRepository();
            var creator = BuildDoctor(1);
            var date = DateTime.Now.AddDays(10);
            fake.AddConflictForStaff(creator.StaffID, date);
            var service = new HangoutService(fake);

            Assert.Throws<InvalidOperationException>(
                () => service.CreateHangout("Team hangout", "Desc", date, 5, creator));

            Assert.Empty(fake.GetAllHangouts());
        }

        [Fact]
        public void JoinHangout_WhenJoinerHasConflictOnHangoutDay_ThrowsAndParticipantListUnchanged()
        {
            var fake = new InMemoryHangoutRepository();
            var service = new HangoutService(fake);
            var creator = BuildDoctor(1);
            var joiner = BuildDoctor(2);
            var date = DateTime.Now.AddDays(10);
            service.CreateHangout("Team hangout", "Desc", date, 5, creator);
            fake.AddConflictForStaff(joiner.StaffID, date);

            Assert.Throws<InvalidOperationException>(() => service.JoinHangout(1, joiner));

            Assert.Single(fake.GetHangoutById(1)!.ParticipantList);
        }

        [Fact]
        public void JoinHangout_WhenHangoutReachesCapacity_LaterJoinerIsRejected()
        {
            var fake = new InMemoryHangoutRepository();
            var service = new HangoutService(fake);
            var creator = BuildDoctor(1);
            var date = DateTime.Now.AddDays(10);
            service.CreateHangout("Team hangout", "Desc", date, 2, creator);
            service.JoinHangout(1, BuildDoctor(2));

            var lateComer = BuildDoctor(3);
            var ex = Assert.Throws<InvalidOperationException>(() => service.JoinHangout(1, lateComer));

            Assert.Equal("This hangout is already full.", ex.Message);
            Assert.Equal(2, fake.GetHangoutById(1)!.ParticipantList.Count);
        }

        private static Doctor BuildDoctor(int staffId)
            => new Doctor(staffId, "First", "Last", "email@example.com", string.Empty, true, "Cardiology", "LIC-1", DoctorStatus.AVAILABLE, 3);
    }
}
