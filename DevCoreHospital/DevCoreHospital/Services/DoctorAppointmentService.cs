using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using Microsoft.Data.SqlClient;

namespace DevCoreHospital.Services;

public class DoctorAppointmentService : IDoctorAppointmentService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DoctorAppointmentService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(
        int doctorId,
        DateTime fromDate,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var result = new List<Appointment>();

        const string sql = """
            SELECT
                a.id,
                a.doctor_id,
                a.[date],
                a.start_time,
                a.end_time,
                a.[status],
                a.[type],
                a.[location]
            FROM Appointment a
            WHERE a.doctor_id = @doctorId
              AND (
                    a.[date] > @fromDate
                    OR (a.[date] = @fromDate AND a.end_time >= @fromTime)
                  )
            ORDER BY a.[date] ASC, a.start_time ASC
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
            """;

        await using SqlConnection connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@doctorId", doctorId);
        command.Parameters.AddWithValue("@fromDate", fromDate.Date);
        command.Parameters.AddWithValue("@fromTime", fromDate.TimeOfDay);
        command.Parameters.AddWithValue("@skip", skip);
        command.Parameters.AddWithValue("@take", take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Appointment
            {
                Id = reader.GetInt32(0),
                DoctorId = reader.GetInt32(1),
                Date = reader.GetDateTime(2),
                StartTime = reader.GetTimeSpan(3),
                EndTime = reader.GetTimeSpan(4),
                Status = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Type = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Location = reader.IsDBNull(7) ? "" : reader.GetString(7)
            });
        }

        return result;
    }
}