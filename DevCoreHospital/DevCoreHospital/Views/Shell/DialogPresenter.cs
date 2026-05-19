using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views.Shell
{
    public sealed class DialogPresenter
    {
        private const string CloseButtonLabel = "OK";

        private XamlRoot? xamlRoot;

        public void SetXamlRoot(XamlRoot xamlRoot) => this.xamlRoot = xamlRoot;

        public async Task ShowMessageAsync(string title, string message)
        {
            if (xamlRoot == null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = CloseButtonLabel,
                XamlRoot = xamlRoot,
            };

            await dialog.ShowAsync();
        }
    }
}
