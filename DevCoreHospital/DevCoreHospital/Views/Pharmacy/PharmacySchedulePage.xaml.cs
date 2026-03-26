using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Pharmacy;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views.Pharmacy;

public sealed partial class PharmacySchedulePage : Page
{
    public PharmacyScheduleViewModel ViewModel { get; }

    public PharmacySchedulePage()
    {
        InitializeComponent();

        ICurrentUserService currentUser = new CurrentUserService();
        var sqlFactory = new SqlConnectionFactory();
        var dbManager = new DatabaseManager(sqlFactory);
        var staffRepo = new StaffRepository(dbManager);
        var shiftRepo = new ShiftRepository(dbManager);
        var scheduleService = new PharmacyScheduleService(staffRepo, shiftRepo);
        var handoverService = new PharmacyHandoverService(sqlFactory);
        ViewModel = new PharmacyScheduleViewModel(currentUser, scheduleService, handoverService);
        DataContext = ViewModel;

        Loaded += PharmacySchedulePage_Loaded;
    }

    private async void PharmacySchedulePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= PharmacySchedulePage_Loaded;
        await ViewModel.InitializeAsync();
    }
}
