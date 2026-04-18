using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class FatigueAuditPage : Page
    {
        private readonly FatigueShiftAuditViewModel _viewModel;

        public FatigueAuditPage()
        {
            InitializeComponent();

            var sqlDataSource = new SqlFatigueShiftDataSource(AppSettings.ConnectionString);
            IFatigueAuditRepository auditRepository = new FatigueAuditRepository(sqlDataSource);
            IFatigueAuditService auditService = new FatigueAuditService(auditRepository);

            _viewModel = new FatigueShiftAuditViewModel(auditService);
            DataContext = _viewModel;

            WeekStartPicker.Date = new DateTimeOffset(DateTime.Today);
        }

        private void WeekStartPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (sender.Date.HasValue)
            {
                _viewModel.SelectedWeekStart = sender.Date.Value;
            }
        }

        private void RunAutoAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.RunAutoAudit();
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error during audit: {ex.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Violations.Clear();
            _viewModel.Suggestions.Clear();
        }

        private async void ApplyReassignment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int shiftId)
            {
                var result = _viewModel.ApplyReassignment(shiftId);

                var dialog = new ContentDialog
                {
                    Title = result.Title,
                    Content = result.Message,
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void PublishRoster_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CanPublish)
            {
                var dialog = new ContentDialog
                {
                    Title = "Roster Published",
                    Content = $"The roster for the {_viewModel.WeekLabel} has been published successfully.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }
    }
}
