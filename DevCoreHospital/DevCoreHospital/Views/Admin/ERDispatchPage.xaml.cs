using DevCoreHospital.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views
{
    public sealed partial class ERDispatchPage : Page
    {
        private const int SimulatedIncomingRequestCount = 3;

        public ERDispatchViewModel ViewModel { get; }

        public ERDispatchPage()
        {
            InitializeComponent();

            ViewModel = App.Services.GetRequiredService<ERDispatchViewModel>();
            DataContext = ViewModel;
        }

        private async void RunDispatch_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RunDispatchAsync();

            if (ViewModel.UnmatchedRequests.Count > 0)
            {
                UnmatchedRequestCombo.SelectedIndex = 0;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Refresh();
            UnmatchedRequestCombo.SelectedIndex = -1;
            OverrideDoctorCombo.SelectedIndex = -1;
        }

        private async void SimulateIncoming_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SimulateIncomingAsync(SimulatedIncomingRequestCount);
        }

        private async void UnmatchedRequestCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is ERDispatchViewModel.UnmatchedRequestRow row)
            {
                await ViewModel.LoadOverrideCandidatesAsync(row.RequestId);
            }
        }

        private async void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is not ERDispatchViewModel.UnmatchedRequestRow selectedRequest)
            {
                return;
            }

            if (OverrideDoctorCombo.SelectedItem is not ERDispatchViewModel.OverrideCandidateRow candidate)
            {
                return;
            }

            var success = await ViewModel.ApplyOverrideAsync(selectedRequest.RequestId, candidate.DoctorId);
            if (!success)
            {
                return;
            }

            OverrideDoctorCombo.SelectedIndex = -1;
            if (ViewModel.UnmatchedRequests.Count > 0)
            {
                UnmatchedRequestCombo.SelectedIndex = 0;
            }
            else
            {
                UnmatchedRequestCombo.SelectedIndex = -1;
            }
        }
    }
}
