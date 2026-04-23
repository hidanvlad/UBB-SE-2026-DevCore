using System;
using DevCoreHospital.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class FatigueAuditPage : Page
    {
        private readonly FatigueShiftAuditViewModel viewModel;

        public FatigueAuditPage()
        {
            InitializeComponent();

            viewModel = App.Services.GetRequiredService<FatigueShiftAuditViewModel>();
            DataContext = viewModel;

            WeekStartPicker.Date = new System.DateTimeOffset(System.DateTime.Today);
        }

        private void WeekStartPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (sender.Date.HasValue)
            {
                viewModel.SelectedWeekStart = sender.Date.Value;
            }
        }

        private void RunAutoAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                viewModel.RunAutoAudit();
            }
            catch (System.Exception ex)
            {
                viewModel.StatusMessage = $"Error during audit: {ex.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Violations.Clear();
            viewModel.Suggestions.Clear();
        }

        private async void ApplyReassignment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int shiftId)
            {
                var result = viewModel.ApplyReassignment(shiftId);

                var dialog = new ContentDialog
                {
                    Title = result.title,
                    Content = result.message,
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void PublishRoster_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.CanPublish)
            {
                var dialog = new ContentDialog
                {
                    Title = "Roster Published",
                    Content = $"The roster for the {viewModel.WeekLabel} has been published successfully.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }
    }
}
