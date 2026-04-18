using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Pharmacy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views.Pharmacy;

public sealed partial class PharmacistVacationPage : Page
{
    public PharmacistVacationViewModel ViewModel { get; }

    public PharmacistVacationPage()
    {
        InitializeComponent();

        var dbManager = new DatabaseManager(AppSettings.ConnectionString);
        var staffRepository = new StaffRepository(dbManager);
        var shiftRepository = new ShiftRepository(dbManager);

        IPharmacyVacationService service = new PharmacyVacationService(staffRepository, shiftRepository);
        ViewModel = new PharmacistVacationViewModel(service);

        DataContext = ViewModel;
        PharmacistComboBox.ItemsSource = ViewModel.Pharmacists;
    }

    private void AddVacationShift_Click(object sender, RoutedEventArgs e)
    {
        var selected = PharmacistComboBox.SelectedItem as PharmacistVacationViewModel.PharmacistChoice;

        var result = ViewModel.TryRegisterVacation(
            selected,
            StartDatePicker.Date,
            EndDatePicker.Date);

        ShowMessage(result.Message, MapSeverity(result.Status));
    }

    private static InfoBarSeverity MapSeverity(VacationRegistrationStatus status) => status switch
    {
        VacationRegistrationStatus.Success => InfoBarSeverity.Success,
        VacationRegistrationStatus.Warning => InfoBarSeverity.Warning,
        _ => InfoBarSeverity.Error
    };

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
