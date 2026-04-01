using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class ERDispatchPage : Page
    {
        public ERDispatchViewModel ViewModel { get; }

        public ERDispatchPage()
        {
            InitializeComponent();

            var dataSource = new SqlERDispatchDataSource(AppSettings.ConnectionString);
            var repository = new ERDispatchRepository(dataSource);
            var dispatchService = new ERDispatchService(repository);

            ViewModel = new ERDispatchViewModel(dispatchService);
            DataContext = ViewModel;
        }

        private async void RunDispatch_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RunDispatchAsync();

            if (ViewModel.UnmatchedRequests.Count > 0)
                UnmatchedRequestCombo.SelectedIndex = 0;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Refresh();
            UnmatchedRequestCombo.SelectedIndex = -1;
            OverrideDoctorCombo.SelectedIndex = -1;
        }

        private async void SimulateIncoming_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SimulateIncomingAsync(3);
        }

        private async void UnmatchedRequestCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is ERDispatchViewModel.UnmatchedRequestRow row)
                await ViewModel.LoadOverrideCandidatesAsync(row.RequestId);
        }

        private async void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is not ERDispatchViewModel.UnmatchedRequestRow req)
            {
                return;
            }

            if (OverrideDoctorCombo.SelectedItem is not ERDispatchViewModel.OverrideCandidateRow candidate)
            {
                return;
            }

            var success = await ViewModel.ApplyOverrideAsync(req.RequestId, candidate.DoctorId);
            if (!success)
                return;

            OverrideDoctorCombo.SelectedIndex = -1;
            if (ViewModel.UnmatchedRequests.Count > 0)
                UnmatchedRequestCombo.SelectedIndex = 0;
            else
                UnmatchedRequestCombo.SelectedIndex = -1;
        }
    }
}
