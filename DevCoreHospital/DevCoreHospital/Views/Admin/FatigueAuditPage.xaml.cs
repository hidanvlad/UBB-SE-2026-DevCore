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

            WeekStartPicker.Date = new DateTimeOffset(DateTime.Today);
        }

        private void WeekStartPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs eventArgs)
        {
            if (sender.Date.HasValue)
            {
                viewModel.SelectedWeekStart = sender.Date.Value;
            }
        }

        private void RunAutoAudit_Click(object sender, RoutedEventArgs eventArgs)
        {
            try
            {
                viewModel.RunAutoAudit();
            }
            catch (Exception exception)
            {
                viewModel.StatusMessage = $"Error during audit: {exception.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs eventArgs)
        {
            viewModel.Violations.Clear();
            viewModel.Suggestions.Clear();
        }

        private async void ApplyReassignment_Click(object sender, RoutedEventArgs eventArgs)
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

        private void PublishRoster_Click(object sender, RoutedEventArgs eventArgs)
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
