using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DevCoreHospital.ViewModels;
using System;

namespace DevCoreHospital.Views
{
    public sealed partial class MedicalEvaluationView : Page
    {
        public MedicalEvaluationViewModel ViewModel { get; } = new MedicalEvaluationViewModel();

        public MedicalEvaluationView()
        {
            this.InitializeComponent();

            this.DataContext = ViewModel;
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {

            ContentDialog deleteDialog = new ContentDialog
            {
                Title = "Confirm Deletion",
                Content = "This action is permanent. Are you sure you want to delete this diagnosis?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
               
                XamlRoot = this.Content.XamlRoot
            };

            ContentDialogResult result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.ExecuteDeletion();
            }
        }
    }
}