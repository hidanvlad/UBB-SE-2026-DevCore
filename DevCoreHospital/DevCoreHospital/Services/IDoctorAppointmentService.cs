using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services;

public interface IDoctorAppointmentService
{
    Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(
        int doctorId,
        DateTime fromDate,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default);
}