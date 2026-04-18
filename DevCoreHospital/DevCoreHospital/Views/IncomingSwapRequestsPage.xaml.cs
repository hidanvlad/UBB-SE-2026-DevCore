using System.Linq;
using DevCoreHospital.Configuration;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml.Controls;
using DoctorModel = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.Views
{
    public sealed partial class IncomingSwapRequestsPage : Page
    {
        public IncomingSwapRequestsViewModel ViewModel { get; }

        public IncomingSwapRequestsPage()
        {
            this.InitializeComponent();

            var staffRepo = new StaffRepository(AppSettings.ConnectionString);
            var shiftRepo = new ShiftRepository(AppSettings.ConnectionString, staffRepo);
            var shiftSwapRepository = new ShiftSwapRepository(AppSettings.ConnectionString);
            var service = new ShiftSwapService(staffRepo, shiftRepo, shiftSwapRepository);

            var doctors = staffRepo
                .LoadAllStaff()
                .OfType<DoctorModel>()
                .OrderBy(doctor => doctor.FirstName)
                .ThenBy(doctor => doctor.LastName)
                .Select(doctor => new DoctorOptionViewModel
                {
                    StaffId = doctor.StaffID,
                    DisplayName = $"{doctor.FirstName} {doctor.LastName}".Trim()
                });

            ViewModel = new IncomingSwapRequestsViewModel(service, doctors);
            DataContext = ViewModel;
        }
    }
}
