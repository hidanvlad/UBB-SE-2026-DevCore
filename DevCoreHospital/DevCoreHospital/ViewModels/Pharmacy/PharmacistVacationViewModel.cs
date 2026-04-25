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
        private readonly IPharmacyVacationService service;

        public ObservableCollection<PharmacistChoice> Pharmacists { get; } = new ObservableCollection<PharmacistChoice>();

        public PharmacistVacationViewModel(IPharmacyVacationService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            LoadPharmacists();
        }

        public void LoadPharmacists()
        {
            Pharmacists.Clear();
            foreach (var pharmacist in service.GetPharmacists())
            {
                bool IsNonEmpty(string? namePart) => !string.IsNullOrWhiteSpace(namePart);
                var displayName = string.Join(
                    " ",
                    new[] { pharmacist.FirstName?.Trim(), pharmacist.LastName?.Trim() }
                        .Where(IsNonEmpty));
                Pharmacists.Add(new PharmacistChoice(pharmacist, displayName));
            }
        }

        public VacationRegistrationResult TryRegisterVacation(
            PharmacistChoice? pharmacist,
            DateTimeOffset? startDate,
            DateTimeOffset? endDate)
        {
            if (pharmacist is null)
            {
                return VacationRegistrationResult.Warning("Select a pharmacist first.");
            }

            if (startDate is null || endDate is null)
            {
                return VacationRegistrationResult.Warning("Select both start and end dates.");
            }

            try
            {
                service.RegisterVacation(
                    pharmacist.staff.StaffID,
                    startDate.Value.Date,
                    endDate.Value.Date);
                return VacationRegistrationResult.Success("Vacation shift added to repository.");
            }
            catch (ArgumentException exception)
            {
                return VacationRegistrationResult.Error(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return VacationRegistrationResult.Error(exception.Message);
            }
        }

        public sealed record PharmacistChoice(Pharmacyst staff, string displayName);
    }

    public sealed record VacationRegistrationResult(
        VacationRegistrationStatus status,
        string message)
    {
        public static VacationRegistrationResult Success(string message) =>
            new VacationRegistrationResult(VacationRegistrationStatus.Success, message);

        public static VacationRegistrationResult Warning(string message) =>
            new VacationRegistrationResult(VacationRegistrationStatus.Warning, message);

        public static VacationRegistrationResult Error(string message) =>
            new VacationRegistrationResult(VacationRegistrationStatus.Error, message);
    }

    public enum VacationRegistrationStatus
    {
        Success,
        Warning,
        Error,
    }
}
