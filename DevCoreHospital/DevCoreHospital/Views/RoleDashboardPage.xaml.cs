using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Views.Admin;
using DevCoreHospital.Views.Doctor;
using DevCoreHospital.Views.Pharmacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace DevCoreHospital.Views
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public sealed partial class RoleDashboardPage : Page
    {
        private readonly ICurrentUserService currentUser;
        private readonly ObservableCollection<MenuEntry> items = new ObservableCollection<MenuEntry>();
        private readonly Dictionary<string, Type> routes = new Dictionary<string, Type>();

        public RoleDashboardPage()
        {
            InitializeComponent();

            currentUser = App.Services.GetRequiredService<ICurrentUserService>();
            MenuList.ItemsSource = items;
            BuildForRole();
        }

        private void BuildForRole()
        {
            items.Clear();
            routes.Clear();

            RoleText.Text = $"Role: {currentUser.RoleType}";

            switch (currentUser.RoleType)
            {
                case UserRole.Admin:
                    Add("See Doctor Schedule", "admin-doctor-schedule", typeof(DoctorSchedulePage));
                    Add("See Pharmacy Schedule", "admin-pharmacy-schedule", typeof(PharmacySchedulePage));
                    Add("Appointments", "admin-appointments", typeof(AppointmentsPage));
                    Add("Create Shift", "admin-create-shift", typeof(AdminShiftView));
                    Add("Auto-Audit", "admin-auto-audit", typeof(FatigueAuditPage));
                    Add("ER Dispatch", "admin-er-dispatch", typeof(ERDispatchPage));
                    break;

                case UserRole.Pharmacist:
                    Add("See Schedule", "pharmacist-schedule", typeof(PharmacySchedulePage));
                    Add("Vacation Window", "pharmacist-vacation", typeof(PharmacistVacationPage));
                    Add("Salary", "pharmacist-salary", typeof(DevCoreHospital.Views.SalaryPlaceholderPage));
                    break;

                case UserRole.Doctor:
                    Add("Medical Evaluation", "doctor-medical", typeof(DevCoreHospital.Views.MedicalEvaluationView));
                    Add("Shift Swap Request", "doctor-shift-swap-request", typeof(MySchedulePage));
                    Add("Incoming Swap Requests", "doctor-shift-swap-incoming", typeof(IncomingSwapRequestsPage));
                    Add("See Schedule", "doctor-schedule", typeof(DoctorSchedulePage));
                    Add("Salary", "doctor-salary", typeof(DevCoreHospital.Views.SalaryPlaceholderPage));
                    Add("Hang Out", "doctor-hangout", typeof(HangOutPlaceholderPage));
                    break;
            }

            var first = items.FirstOrDefault();
            if (first != null)
            {
                MenuList.SelectedItem = first;
                NavigateToKey(first.Key);
            }
        }

        private void Add(string title, string key, Type pageType)
        {
            if (!typeof(Page).IsAssignableFrom(pageType))
            {
                throw new InvalidOperationException($"{pageType.FullName} is not a Page.");
            }

            items.Add(new MenuEntry { Key = key, Title = title });
            routes[key] = pageType;
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuList.SelectedItem is not MenuEntry entry)
            {
                return;
            }

            NavigateToKey(entry.Key);
        }

        private void NavigateToKey(string key)
        {
            if (!routes.TryGetValue(key, out var pageType))
            {
                pageType = typeof(NotImplementedPlaceholderPage);
            }

            ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
        }

        private void ChangeRole_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(RoleSelectionPage));
        }

        private sealed class MenuEntry
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }
    }
}
