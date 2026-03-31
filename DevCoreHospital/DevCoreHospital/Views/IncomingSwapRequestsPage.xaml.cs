using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using DoctorModel = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.Views
{
    public sealed partial class IncomingSwapRequestsPage : Page
    {
        public IncomingSwapRequestsViewModel ViewModel { get; }

        public IncomingSwapRequestsPage()
        {
            this.InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var staffRepo = new StaffRepository(dbManager);
            var shiftRepo = new ShiftRepository(dbManager);
            var service = new StaffAndShiftService(staffRepo, shiftRepo, dbManager);

            var doctors = staffRepo
                .LoadAllStaff()
                .OfType<DoctorModel>()
                .OrderBy(d => d.FirstName)
                .ThenBy(d => d.LastName)
                .Select(d => new DoctorOptionViewModel
                {
                    StaffId = d.StaffID,
                    DisplayName = $"{d.FirstName} {d.LastName}".Trim()
                });

            ViewModel = new IncomingSwapRequestsViewModel(service, doctors);
            DataContext = ViewModel;
        }
    }
}