using System;
using DevCoreHospital.Models;
using Xunit;

namespace DevCoreHospital.Tests.Models
{
    public class ShiftSwapRequestTests
    {
        [Fact]
        public void DefaultConstructor_SetsStatusToPending()
        {
            // Arrange / Act
            var request = new ShiftSwapRequest();

            // Assert
            Assert.Equal(ShiftSwapRequestStatus.PENDING, request.Status);
        }

        [Fact]
        public void ParameterizedConstructor_SetsAllProperties()
        {
            // Arrange
            var before = DateTime.UtcNow;

            // Act
            var request = new ShiftSwapRequest(swapId: 1, shiftId: 2, requesterId: 3, colleagueId: 4);
            var after = DateTime.UtcNow;

            // Assert
            Assert.Equal(1, request.SwapId);
            Assert.Equal(2, request.ShiftId);
            Assert.Equal(3, request.RequesterId);
            Assert.Equal(4, request.ColleagueId);
            Assert.Equal(ShiftSwapRequestStatus.PENDING, request.Status);
            Assert.InRange(request.RequestedAt, before, after);
        }

        [Fact]
        public void ParameterizedConstructor_SetsStatusToPending()
        {
            // Arrange / Act
            var request = new ShiftSwapRequest(10, 20, 30, 40);

            // Assert
            Assert.Equal(ShiftSwapRequestStatus.PENDING, request.Status);
        }

        [Fact]
        public void Status_CanBeChangedToAccepted()
        {
            // Arrange
            var request = new ShiftSwapRequest(1, 2, 3, 4);

            // Act
            request.Status = ShiftSwapRequestStatus.ACCEPTED;

            // Assert
            Assert.Equal(ShiftSwapRequestStatus.ACCEPTED, request.Status);
        }

        [Fact]
        public void Status_CanBeChangedToRejected()
        {
            // Arrange
            var request = new ShiftSwapRequest(1, 2, 3, 4);

            // Act
            request.Status = ShiftSwapRequestStatus.REJECTED;

            // Assert
            Assert.Equal(ShiftSwapRequestStatus.REJECTED, request.Status);
        }

        [Fact]
        public void Status_CanBeChangedToCancelled()
        {
            // Arrange
            var request = new ShiftSwapRequest(1, 2, 3, 4);

            // Act
            request.Status = ShiftSwapRequestStatus.CANCELLED;

            // Assert
            Assert.Equal(ShiftSwapRequestStatus.CANCELLED, request.Status);
        }

        [Theory]
        [InlineData(ShiftSwapRequestStatus.PENDING)]
        [InlineData(ShiftSwapRequestStatus.ACCEPTED)]
        [InlineData(ShiftSwapRequestStatus.REJECTED)]
        [InlineData(ShiftSwapRequestStatus.CANCELLED)]
        public void ShiftSwapRequestStatus_AllExpectedValuesAreDefined(ShiftSwapRequestStatus status)
        {
            // Arrange / Act
            var request = new ShiftSwapRequest { Status = status };

            // Assert
            Assert.Equal(status, request.Status);
        }
    }
}
