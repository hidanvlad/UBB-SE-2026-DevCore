using System;
using System.Collections.Generic;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using Moq;

namespace DevCoreHospital.Tests.Repositories;

public class FatigueAuditRepositoryTests
{
    private readonly Mock<IFatigueShiftDataSource> dataSource;
    private readonly FatigueAuditRepository repository;

    public FatigueAuditRepositoryTests()
    {
        dataSource = new Mock<IFatigueShiftDataSource>();
        repository = new FatigueAuditRepository(dataSource.Object);
    }

    [Fact]
    public void Constructor_WhenDataSourceIsNull_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => new FatigueAuditRepository(null!));

    [Fact]
    public void GetAllShifts_DelegatesToDataSource()
    {
        dataSource.Setup(d => d.GetAllShifts()).Returns(new List<RosterShift>());

        repository.GetAllShifts();

        dataSource.Verify(d => d.GetAllShifts(), Times.Once);
    }

    [Fact]
    public void GetAllShifts_ReturnsResultFromDataSource()
    {
        var expected = new List<RosterShift>
        {
            new RosterShift { Id = 1, StaffId = 10, StaffName = "Alice" },
            new RosterShift { Id = 2, StaffId = 11, StaffName = "Bob" },
        };
        dataSource.Setup(d => d.GetAllShifts()).Returns(expected);

        var result = repository.GetAllShifts();

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
    }

    [Fact]
    public void GetAllShifts_WhenDataSourceReturnsEmpty_ReturnsEmptyList()
    {
        dataSource.Setup(d => d.GetAllShifts()).Returns(new List<RosterShift>());

        Assert.Empty(repository.GetAllShifts());
    }

    [Fact]
    public void GetStaffProfiles_DelegatesToDataSource()
    {
        dataSource.Setup(d => d.GetStaffProfiles()).Returns(new List<StaffProfile>());

        repository.GetStaffProfiles();

        dataSource.Verify(d => d.GetStaffProfiles(), Times.Once);
    }

    [Fact]
    public void GetStaffProfiles_ReturnsResultFromDataSource()
    {
        var expected = new List<StaffProfile>
        {
            new StaffProfile { StaffId = 1, FullName = "Alice", Role = "Doctor", Specialization = "Cardiology" },
            new StaffProfile { StaffId = 2, FullName = "Bob", Role = "Doctor", Specialization = "Neurology" },
        };
        dataSource.Setup(d => d.GetStaffProfiles()).Returns(expected);

        var result = repository.GetStaffProfiles();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].FullName);
        Assert.Equal("Bob", result[1].FullName);
    }

    [Fact]
    public void GetStaffProfiles_WhenDataSourceReturnsEmpty_ReturnsEmptyList()
    {
        dataSource.Setup(d => d.GetStaffProfiles()).Returns(new List<StaffProfile>());

        Assert.Empty(repository.GetStaffProfiles());
    }

    [Fact]
    public void ReassignShift_DelegatesToDataSource()
    {
        dataSource.Setup(d => d.ReassignShift(5, 10)).Returns(true);

        repository.ReassignShift(5, 10);

        dataSource.Verify(d => d.ReassignShift(5, 10), Times.Once);
    }

    [Fact]
    public void ReassignShift_ReturnsTrue_WhenDataSourceReturnsTrue()
    {
        dataSource.Setup(d => d.ReassignShift(It.IsAny<int>(), It.IsAny<int>())).Returns(true);

        Assert.True(repository.ReassignShift(5, 10));
    }

    [Fact]
    public void ReassignShift_ReturnsFalse_WhenDataSourceReturnsFalse()
    {
        dataSource.Setup(d => d.ReassignShift(It.IsAny<int>(), It.IsAny<int>())).Returns(false);

        Assert.False(repository.ReassignShift(99, 10));
    }

    [Fact]
    public void ReassignShift_PassesCorrectShiftIdAndStaffId()
    {
        dataSource.Setup(d => d.ReassignShift(42, 7)).Returns(true);

        repository.ReassignShift(42, 7);

        dataSource.Verify(d => d.ReassignShift(42, 7), Times.Once);
    }
}
