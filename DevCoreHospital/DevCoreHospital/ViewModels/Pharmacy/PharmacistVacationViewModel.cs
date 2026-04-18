using System;
using System.Collections.ObjectModel;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Pharmacy
{
    public sealed class PharmacistVacationViewModel : ObservableObject
    {
        private readonly IPharmacyVacationService _service;

        public ObservableCollection<PharmacistChoice> Pharmacists { get; } = new();

        public PharmacistVacationViewModel(IPharmacyVacationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            LoadPharmacists();
        }

        public void LoadPharmacists()
        {
            Pharmacists.Clear();
            foreach (var p in _service.GetPharmacists())
            {
                var displayName = string.Join(
                    " ",
                    new[] { p.FirstName?.Trim(), p.LastName?.Trim() }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                Pharmacists.Add(new PharmacistChoice(p, displayName));
            }
        }

        /// <summary>
        /// Registers the vacation for the provided pharmacist. Returns a
        /// <see cref="VacationRegistrationResult"/> describing success/failure
        /// to keep the UI free of business branching.
        /// </summary>
        public VacationRegistrationResult TryRegisterVacation(
            PharmacistChoice? pharmacist,
            DateTimeOffset? startDate,
            DateTimeOffset? endDate)
        {
            if (pharmacist is null)
                return VacationRegistrationResult.Warning("Select a pharmacist first.");

            if (startDate is null || endDate is null)
                return VacationRegistrationResult.Warning("Select both start and end dates.");

            try
            {
                _service.RegisterVacation(
                    pharmacist.Staff.StaffID,
                    startDate.Value.Date,
                    endDate.Value.Date);
                return VacationRegistrationResult.Success("Vacation shift added to repository.");
            }
            catch (ArgumentException ex)
            {
                return VacationRegistrationResult.Error(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return VacationRegistrationResult.Error(ex.Message);
            }
        }

        public sealed record PharmacistChoice(Pharmacyst Staff, string DisplayName);
    }

    public sealed record VacationRegistrationResult(
        VacationRegistrationStatus Status,
        string Message)
    {
        public static VacationRegistrationResult Success(string message) =>
            new(VacationRegistrationStatus.Success, message);

        public static VacationRegistrationResult Warning(string message) =>
            new(VacationRegistrationStatus.Warning, message);

        public static VacationRegistrationResult Error(string message) =>
            new(VacationRegistrationStatus.Error, message);
    }

    public enum VacationRegistrationStatus
    {
        Success,
        Warning,
        Error
    }
}
