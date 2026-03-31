using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class MySchedulePage : Page
    {
        public MyScheduleViewModel ViewModel { get; }

        public MySchedulePage()
        {
            InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var staffRepo = new StaffRepository(dbManager);
            var shiftRepo = new ShiftRepository(dbManager);
            var staffAndShiftService = new StaffAndShiftService(staffRepo, shiftRepo, dbManager);

            ViewModel = new MyScheduleViewModel(staffAndShiftService, shiftRepo, staffRepo);
            DataContext = ViewModel;
        }
    }
}