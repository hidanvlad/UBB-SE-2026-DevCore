using DevCoreHospital.Configuration;
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

            var staffRepo = new StaffRepository(AppSettings.ConnectionString);
            var shiftRepo = new ShiftRepository(AppSettings.ConnectionString, staffRepo);
            var shiftSwapRepository = new ShiftSwapRepository(AppSettings.ConnectionString);
            var shiftSwapService = new ShiftSwapService(staffRepo, shiftRepo, shiftSwapRepository);

            ViewModel = new MyScheduleViewModel(shiftSwapService, shiftRepo, staffRepo);
            DataContext = ViewModel;
        }
    }
}
