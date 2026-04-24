using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.Tests.Repositories;
using Xunit;

namespace DevCoreHospital.Tests.Integration
{
    public class HangoutFlowIntegrationTests : IClassFixture<SqlTestFixture>
    {
        private readonly SqlTestFixture database;
        private static readonly DateTime HangoutDate = DateTime.Today.AddDays(14);

        public HangoutFlowIntegrationTests(SqlTestFixture database) => this.database = database;

        [Fact]
        public void CreateThenJoin_WhenNoConflicts_StoresHangoutWithBothParticipants()
        {
            using var connection = database.OpenConnection();
            var creatorId = database.InsertStaff(connection, "Doctor", "HgCreate", "Creator",  "Cardiology");
            var joinerId  = database.InsertStaff(connection, "Doctor", "HgCreate", "Joiner",   "Cardiology");
            var hangoutId = 0;
            try
            {
                var repository = new HangoutRepository(database.ConnectionString);
                var service = new HangoutService(repository);
                var creator = new Doctor { StaffID = creatorId };
                var joiner  = new Doctor { StaffID = joinerId };

                hangoutId = service.CreateHangout("Team hangout", "Monthly meetup", HangoutDate, 5, creator);
                service.JoinHangout(hangoutId, joiner);

                var hangout = repository.GetHangoutById(hangoutId);
                Assert.NotNull(hangout);
                Assert.Equal(2, hangout!.ParticipantList.Count);
                bool IsCreator(IStaff participant) => participant.StaffID == creatorId;
                bool IsJoiner(IStaff participant) => participant.StaffID == joinerId;
                Assert.Contains(hangout.ParticipantList, IsCreator);
                Assert.Contains(hangout.ParticipantList, IsJoiner);
            }
            finally
            {
                database.DeleteHangoutParticipants(connection, hangoutId);
                database.DeleteHangout(connection, hangoutId);
                database.DeleteStaff(connection, creatorId);
                database.DeleteStaff(connection, joinerId);
            }
        }

        [Fact]
        public void CreateHangout_WhenCreatorHasRealAppointmentOnHangoutDay_ThrowsAndPersistsNothing()
        {
            using var connection = database.OpenConnection();
            var creatorId = database.InsertStaff(connection, "Doctor", "HgConflict", "Creator", "Cardiology");
            var appointmentId = database.InsertAppointment(connection, 0, creatorId,
                HangoutDate.AddHours(9), HangoutDate.AddHours(10));
            try
            {
                var repository = new HangoutRepository(database.ConnectionString);
                var service = new HangoutService(repository);
                var creator = new Doctor { StaffID = creatorId };

                Assert.Throws<InvalidOperationException>(
                    () => service.CreateHangout("Team hangout", "Desc", HangoutDate, 5, creator));

                bool IsOnHangoutDate(Hangout hangout) => hangout.Date.Date == HangoutDate.Date;
                Assert.DoesNotContain(repository.GetAllHangouts(), IsOnHangoutDate);
            }
            finally
            {
                database.DeleteAppointment(connection, appointmentId);
                database.DeleteStaff(connection, creatorId);
            }
        }

        [Fact]
        public void JoinHangout_WhenJoinerHasRealAppointmentOnHangoutDay_ThrowsAndParticipantListUnchanged()
        {
            using var connection = database.OpenConnection();
            var creatorId = database.InsertStaff(connection, "Doctor", "HgJoinConf", "Creator", "Cardiology");
            var joinerId  = database.InsertStaff(connection, "Doctor", "HgJoinConf", "Joiner",  "Cardiology");
            var hangoutId = 0;
            var appointmentId = 0;
            try
            {
                var repository = new HangoutRepository(database.ConnectionString);
                var service = new HangoutService(repository);
                var creator = new Doctor { StaffID = creatorId };
                var joiner  = new Doctor { StaffID = joinerId };

                hangoutId = service.CreateHangout("Team hangout", "Desc", HangoutDate, 5, creator);

                appointmentId = database.InsertAppointment(connection, 0, joinerId,
                    HangoutDate.AddHours(11), HangoutDate.AddHours(12));

                Assert.Throws<InvalidOperationException>(
                    () => service.JoinHangout(hangoutId, joiner));

                Assert.Single(repository.GetHangoutById(hangoutId)!.ParticipantList);
            }
            finally
            {
                database.DeleteAppointment(connection, appointmentId);
                database.DeleteHangoutParticipants(connection, hangoutId);
                database.DeleteHangout(connection, hangoutId);
                database.DeleteStaff(connection, creatorId);
                database.DeleteStaff(connection, joinerId);
            }
        }

        [Fact]
        public void JoinHangout_WhenHangoutReachesCapacity_LaterJoinerIsRejected()
        {
            using var connection = database.OpenConnection();
            var creatorId = database.InsertStaff(connection, "Doctor", "HgFull", "Creator",  "Cardiology");
            var doctor2Id = database.InsertStaff(connection, "Doctor", "HgFull", "Doctor2",  "Cardiology");
            var doctor3Id = database.InsertStaff(connection, "Doctor", "HgFull", "Doctor3",  "Cardiology");
            var hangoutId = 0;
            try
            {
                var repository = new HangoutRepository(database.ConnectionString);
                var service = new HangoutService(repository);

                hangoutId = service.CreateHangout("Team hangout", "Desc", HangoutDate, 2,
                    new Doctor { StaffID = creatorId });

                service.JoinHangout(hangoutId, new Doctor { StaffID = doctor2Id });

                var exception = Assert.Throws<InvalidOperationException>(
                    () => service.JoinHangout(hangoutId, new Doctor { StaffID = doctor3Id }));

                Assert.Equal("This hangout is already full.", exception.Message);
                Assert.Equal(2, repository.GetHangoutById(hangoutId)!.ParticipantList.Count);
            }
            finally
            {
                database.DeleteHangoutParticipants(connection, hangoutId);
                database.DeleteHangout(connection, hangoutId);
                database.DeleteStaff(connection, creatorId);
                database.DeleteStaff(connection, doctor2Id);
                database.DeleteStaff(connection, doctor3Id);
            }
        }
    }
}
