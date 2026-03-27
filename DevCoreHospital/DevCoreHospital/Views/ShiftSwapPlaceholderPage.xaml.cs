using DevCoreHospital.Data;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class ShiftSwapPlaceholderPage : Page
    {
        public FatigueShiftAuditViewModel ViewModel { get; }

        public ShiftSwapPlaceholderPage()
        {
            InitializeComponent();

            ViewModel = new FatigueShiftAuditViewModel(
                new FatigueAuditService(new MockFatigueShiftDataSource()));

            DataContext = ViewModel;
        }

        private void RunAutoAudit_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RunAutoAudit();
        }
    }
}