using DevCoreHospital.Models;
using DevCoreHospital.ViewModels.Pharmacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views.Pharmacy;

public sealed partial class PharmacistVacationPage : Page
{
    public PharmacistVacationViewModel ViewModel { get; }

    public PharmacistVacationPage()
    {
        InitializeComponent();

        ViewModel = App.Services.GetRequiredService<PharmacistVacationViewModel>();
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

        ShowMessage(result.message, MapSeverity(result.status));
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
