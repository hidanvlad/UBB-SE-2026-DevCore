using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using Moq;

namespace DevCoreHospital.Tests.Repositories;

public class ERDispatchRepositoryTests
{
    private readonly Mock<IERDispatchDataSource> dataSource;
    private readonly ERDispatchRepository repository;

    private static readonly DateTime Now = DateTime.Now;
    private static readonly DateTime ShiftStart = Now.AddHours(-1);
    private static readonly DateTime ShiftEnd = Now.AddHours(1);

    public ERDispatchRepositoryTests()
    {
        dataSource = new Mock<IERDispatchDataSource>();
        repository = new ERDispatchRepository(dataSource.Object);
    }

    [Fact]
    public void GetDoctorRoster_ExcludesEntriesWhereRoleIsNotDoctor()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Nurse", ShiftStart, ShiftEnd),
            BuildEntry(2, "pharmacist", ShiftStart, ShiftEnd),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_IncludesEntriesWhereRoleIsDoctor()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd),
        });

        var result = repository.GetDoctorRoster();

        Assert.Single(result);
        Assert.Equal(1, result[0].DoctorId);
    }

    [Fact]
    public void GetDoctorRoster_ExcludesEntriesWithNoSchedule()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", null, null),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_ExcludesEntriesWhereScheduleIsInTheFuture()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", Now.AddHours(2), Now.AddHours(4)),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_ExcludesEntriesWhereScheduleIsInThePast()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", Now.AddHours(-4), Now.AddHours(-2)),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_ExcludesEntriesWhereIsShiftActiveIsFalse()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, isShiftActive: false),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_IncludesEntriesWhereIsShiftActiveIsNull()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, isShiftActive: null),
        });

        Assert.Single(repository.GetDoctorRoster());
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("COMPLETED")]
    [InlineData("VACATION")]
    public void GetDoctorRoster_ExcludesEntriesWithTerminalShiftStatus(string shiftStatus)
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, shiftStatusRaw: shiftStatus),
        });

        Assert.Empty(repository.GetDoctorRoster());
    }

    [Fact]
    public void GetDoctorRoster_NormalizesSpecialization_WhenEmpty()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, specialization: string.Empty),
        });

        Assert.Equal("General", repository.GetDoctorRoster()[0].Specialization);
    }

    [Fact]
    public void GetDoctorRoster_NormalizesStatusRaw_WhenEmpty()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, statusRaw: string.Empty),
        });

        Assert.Equal("OFF_DUTY", repository.GetDoctorRoster()[0].StatusRaw);
    }

    [Fact]
    public void GetDoctorRoster_TrimsWhitespaceFromFullName()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd, fullName: "  Dr. Smith  "),
        });

        Assert.Equal("Dr. Smith", repository.GetDoctorRoster()[0].FullName);
    }

    [Fact]
    public void GetDoctorRoster_DeduplicatesByDoctorId_KeepsEntryWithEarliestScheduleEnd()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(5, "Doctor", ShiftStart, Now.AddHours(3)),
            BuildEntry(5, "Doctor", ShiftStart, Now.AddHours(1)),
        });

        var result = repository.GetDoctorRoster();

        Assert.Single(result);
        Assert.Equal(Now.AddHours(1).Hour, result[0].ScheduleEnd!.Value.Hour);
    }

    [Fact]
    public void GetDoctorRoster_ReturnsMultipleDoctors_WhenDistinctIds()
    {
        dataSource.Setup(d => d.GetRosterEntries()).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd),
            BuildEntry(2, "Doctor", ShiftStart, ShiftEnd),
        });

        Assert.Equal(2, repository.GetDoctorRoster().Count);
    }

    [Fact]
    public void GetPendingRequests_ReturnsOnlyPendingStatus()
    {
        dataSource.Setup(d => d.GetRequests()).Returns(new List<ERRequest>
        {
            new ERRequest { Id = 1, Status = "PENDING", CreatedAt = Now },
            new ERRequest { Id = 2, Status = "ASSIGNED", CreatedAt = Now },
            new ERRequest { Id = 3, Status = "CLOSED", CreatedAt = Now },
        });

        var result = repository.GetPendingRequests();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void GetPendingRequests_OrdersByCreatedAtAscending()
    {
        dataSource.Setup(d => d.GetRequests()).Returns(new List<ERRequest>
        {
            new ERRequest { Id = 3, Status = "PENDING", CreatedAt = Now.AddMinutes(10) },
            new ERRequest { Id = 1, Status = "PENDING", CreatedAt = Now.AddMinutes(-10) },
            new ERRequest { Id = 2, Status = "PENDING", CreatedAt = Now },
        });

        var result = repository.GetPendingRequests();

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void GetPendingRequests_IsCaseInsensitiveForStatus()
    {
        dataSource.Setup(d => d.GetRequests()).Returns(new List<ERRequest>
        {
            new ERRequest { Id = 1, Status = "pending", CreatedAt = Now },
            new ERRequest { Id = 2, Status = "Pending", CreatedAt = Now },
        });

        Assert.Equal(2, repository.GetPendingRequests().Count);
    }

    [Fact]
    public void GetPendingRequests_WhenNoRequests_ReturnsEmpty()
    {
        dataSource.Setup(d => d.GetRequests()).Returns(new List<ERRequest>());

        Assert.Empty(repository.GetPendingRequests());
    }

    [Fact]
    public void CreateIncomingRequest_CallsDataSourceWithPendingStatus()
    {
        dataSource.Setup(d => d.CreateRequest("Cardiology", "ER", "PENDING")).Returns(42);

        var id = repository.CreateIncomingRequest("Cardiology", "ER");

        Assert.Equal(42, id);
        dataSource.Verify(d => d.CreateRequest("Cardiology", "ER", "PENDING"), Times.Once);
    }

    [Fact]
    public void GetRequestById_DelegatesToDataSource()
    {
        var expected = new ERRequest { Id = 7, Status = "PENDING" };
        dataSource.Setup(d => d.GetRequestById(7)).Returns(expected);

        Assert.Same(expected, repository.GetRequestById(7));
    }

    [Fact]
    public void GetRequestById_ReturnsNull_WhenNotFound()
    {
        dataSource.Setup(d => d.GetRequestById(99)).Returns((ERRequest?)null);

        Assert.Null(repository.GetRequestById(99));
    }

    [Fact]
    public void GetDoctorById_ReturnsNull_WhenNoMatchingEntryOnCurrentShift()
    {
        dataSource.Setup(d => d.GetRosterEntriesByStaffId(1)).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", Now.AddHours(2), Now.AddHours(4)),
        });

        Assert.Null(repository.GetDoctorById(1));
    }

    [Fact]
    public void GetDoctorById_ReturnsNull_WhenEntryIsNotDoctor()
    {
        dataSource.Setup(d => d.GetRosterEntriesByStaffId(1)).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Nurse", ShiftStart, ShiftEnd),
        });

        Assert.Null(repository.GetDoctorById(1));
    }

    [Fact]
    public void GetDoctorById_ReturnsEntry_WhenOnCurrentShift()
    {
        dataSource.Setup(d => d.GetRosterEntriesByStaffId(1)).Returns(new List<DoctorRosterEntry>
        {
            BuildEntry(1, "Doctor", ShiftStart, ShiftEnd),
        });

        var result = repository.GetDoctorById(1);

        Assert.NotNull(result);
        Assert.Equal(1, result!.DoctorId);
    }

    [Fact]
    public void UpdateRequestStatus_DelegatesToDataSource()
    {
        repository.UpdateRequestStatus(5, "ASSIGNED", 10, "Dr. Smith");

        dataSource.Verify(d => d.UpdateRequestStatus(5, "ASSIGNED", 10, "Dr. Smith"), Times.Once);
    }

    [Fact]
    public void UpdateDoctorStatus_DelegatesToDataSource()
    {
        repository.UpdateDoctorStatus(3, DoctorStatus.IN_EXAMINATION);

        dataSource.Verify(d => d.UpdateDoctorStatus(3, DoctorStatus.IN_EXAMINATION), Times.Once);
    }

    private static DoctorRosterEntry BuildEntry(
        int doctorId, string role, DateTime? scheduleStart, DateTime? scheduleEnd,
        bool? isShiftActive = true, string shiftStatusRaw = "SCHEDULED",
        string specialization = "Cardiology", string statusRaw = "AVAILABLE",
        string fullName = "Dr. Test")
        => new DoctorRosterEntry
        {
            DoctorId = doctorId,
            RoleRaw = role,
            ScheduleStart = scheduleStart,
            ScheduleEnd = scheduleEnd,
            IsShiftActive = isShiftActive,
            ShiftStatusRaw = shiftStatusRaw,
            Specialization = specialization,
            StatusRaw = statusRaw,
            FullName = fullName,
            Location = "ER",
        };
}
