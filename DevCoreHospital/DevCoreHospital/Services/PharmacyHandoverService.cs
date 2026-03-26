using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Services;

public sealed class PharmacyHandoverService : IPharmacyHandoverService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PharmacyHandoverService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> GetProcessingQueueCountAsync(int responsibleStaffId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Pending_Medications
            WHERE ResponsibleStaffID = @sid
              AND OrderStatus = N'Processing';
            """;

        await using SqlConnection connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@sid", responsibleStaffId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is int i ? i : Convert.ToInt32(scalar);
    }

    public async Task<IReadOnlyList<PharmacyStaffMember>> GetAvailableIncomingPharmacistsAsync(
        int outgoingStaffId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT StaffID, DisplayName
            FROM dbo.PharmacyStaff
            WHERE StaffID <> @outgoing
              AND IsAvailable = 1
            ORDER BY DisplayName;
            """;

        var list = new List<PharmacyStaffMember>();

        await using SqlConnection connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@outgoing", outgoingStaffId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new PharmacyStaffMember
            {
                StaffId = reader.GetInt32(0),
                DisplayName = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return list;
    }

    public async Task CompleteShiftHandoverAsync(int outgoingStaffId, int incomingStaffId, CancellationToken cancellationToken = default)
    {
        if (incomingStaffId <= 0)
            throw new InvalidOperationException("Select an incoming pharmacist before completing your shift.");

        if (incomingStaffId == outgoingStaffId)
            throw new InvalidOperationException("Incoming pharmacist must be a different person.");

        await using SqlConnection connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using SqlTransaction tx = connection.BeginTransaction();

        try
        {
            // Verify incoming exists and is available
            const string verifyIncoming = """
                SELECT COUNT(*) FROM dbo.PharmacyStaff
                WHERE StaffID = @incoming AND IsAvailable = 1;
                """;
            await using (SqlCommand v = new(verifyIncoming, connection, tx))
            {
                v.Parameters.AddWithValue("@incoming", incomingStaffId);
                var ok = await v.ExecuteScalarAsync(cancellationToken);
                var n = ok is int i ? i : Convert.ToInt32(ok);
                if (n == 0)
                    throw new InvalidOperationException("Selected incoming pharmacist is not available.");
            }

            // Pending medications: Processing only (not Completed)
            const string reassign = """
                UPDATE dbo.Pending_Medications
                SET ResponsibleStaffID = @incoming
                WHERE ResponsibleStaffID = @outgoing
                  AND OrderStatus = N'Processing';
                """;
            await using (SqlCommand u1 = new(reassign, connection, tx))
            {
                u1.Parameters.AddWithValue("@incoming", incomingStaffId);
                u1.Parameters.AddWithValue("@outgoing", outgoingStaffId);
                await u1.ExecuteNonQueryAsync(cancellationToken);
            }

            const string markUnavailable = """
                UPDATE dbo.PharmacyStaff
                SET IsAvailable = 0
                WHERE StaffID = @outgoing;
                """;
            await using (SqlCommand u2 = new(markUnavailable, connection, tx))
            {
                u2.Parameters.AddWithValue("@outgoing", outgoingStaffId);
                await u2.ExecuteNonQueryAsync(cancellationToken);
            }

            const string completeShift = """
                UPDATE dbo.PharmacyShifts
                SET Status = N'COMPLETED'
                WHERE StaffID = @outgoing
                  AND Status = N'Active';
                """;
            await using (SqlCommand u3 = new(completeShift, connection, tx))
            {
                u3.Parameters.AddWithValue("@outgoing", outgoingStaffId);
                await u3.ExecuteNonQueryAsync(cancellationToken);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
