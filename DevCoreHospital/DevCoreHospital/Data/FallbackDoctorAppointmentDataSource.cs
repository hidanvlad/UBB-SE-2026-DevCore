using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.Data
{
    public sealed class FallbackDoctorAppointmentDataSource : IDoctorAppointmentDataSource
    {
        private const int PrimaryDoctorsTimeoutMs = 2000;

        private readonly IDoctorAppointmentDataSource _primary;
        private readonly IDoctorAppointmentDataSource _fallback;
        private bool _useFallback;

        public FallbackDoctorAppointmentDataSource(IDoctorAppointmentDataSource primary, IDoctorAppointmentDataSource fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            if (_useFallback)
                return await _fallback.GetAllDoctorsAsync();

            try
            {
                var primaryTask = _primary.GetAllDoctorsAsync();
                var completed = await Task.WhenAny(primaryTask, Task.Delay(PrimaryDoctorsTimeoutMs));
                if (completed != primaryTask)
                {
                    _useFallback = true;
                    return await _fallback.GetAllDoctorsAsync();
                }

                var doctors = await primaryTask;
                var hasVisibleNames = doctors.Count > 0 && doctors.Any(d => !string.IsNullOrWhiteSpace(d.DoctorName));
                if (hasVisibleNames)
                    return doctors;

                _useFallback = true;
                return await _fallback.GetAllDoctorsAsync();
            }
            catch
            {
                _useFallback = true;
                return await _fallback.GetAllDoctorsAsync();
            }
        }

        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take) =>
            TryPrimaryThenFallback(
                dataSource => dataSource.GetUpcomingAppointmentsAsync(doctorUserId, fromDate, skip, take));

        public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId) =>
            TryPrimaryThenFallback(dataSource => dataSource.GetAppointmentDetailsAsync(appointmentId));

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId) =>
            TryPrimaryThenFallback(dataSource => dataSource.GetAppointmentsForAdminAsync(doctorId));

        public Task AddAppointmentAsync(Appointment appt) =>
            TryPrimaryThenFallback(dataSource => dataSource.AddAppointmentAsync(appt));

        public Task UpdateAppointmentStatusAsync(int id, string status) =>
            TryPrimaryThenFallback(dataSource => dataSource.UpdateAppointmentStatusAsync(id, status));

        public Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId) =>
            TryPrimaryThenFallback(dataSource => dataSource.GetActiveAppointmentsCountForDoctorAsync(doctorId));

        public Task UpdateDoctorStatusAsync(int doctorId, string status) =>
            TryPrimaryThenFallback(dataSource => dataSource.UpdateDoctorStatusAsync(doctorId, status));

        private async Task<T> TryPrimaryThenFallback<T>(Func<IDoctorAppointmentDataSource, Task<T>> operation)
        {
            if (_useFallback)
                return await operation(_fallback);

            try
            {
                return await operation(_primary);
            }
            catch
            {
                _useFallback = true;
                return await operation(_fallback);
            }
        }

        private async Task TryPrimaryThenFallback(Func<IDoctorAppointmentDataSource, Task> operation)
        {
            if (_useFallback)
            {
                await operation(_fallback);
                return;
            }

            try
            {
                await operation(_primary);
            }
            catch
            {
                _useFallback = true;
                await operation(_fallback);
            }
        }
    }
}

