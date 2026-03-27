using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.Views.Admin;
using DevCoreHospital.Views.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DevCoreHospital.Views
{
    public sealed partial class RoleDashboardPage : Page
    {
        private readonly ICurrentUserService _currentUser = new CurrentUserService();
        private readonly ObservableCollection<MenuEntry> _items = new();
        private readonly Dictionary<string, Type> _routes = new();

        public RoleDashboardPage()
        {
            InitializeComponent();
            MenuList.ItemsSource = _items;
            BuildForRole();
        }

        private void BuildForRole()
        {
            _items.Clear();
            _routes.Clear();

            RoleText.Text = $"Role: {_currentUser.RoleType}";

            switch (_currentUser.RoleType)
            {
                case UserRole.Admin:
                    Add("See Schedule", "admin-schedule", typeof(DoctorSchedulePage));
                    Add("Appointments", "admin-appointments", typeof(AppointmentsPage));
                    Add("Create Shift", "admin-create-shift", typeof(AdminShiftView));
                    Add("Auto-Audit", "admin-auto-audit", typeof(FatigueAuditPage));
                    Add("ER Dispatch", "admin-er-dispatch", typeof(ERDispatchPage));
                    break;

                case UserRole.Pharmacist:
                    Add("See Schedule", "pharmacist-schedule", typeof(DoctorSchedulePage));
                    Add("Salary", "pharmacist-salary", typeof(DevCoreHospital.Views.SalaryPlaceholderPage));
                    break;

                case UserRole.Doctor:
                    Add("Medical Evaluation", "doctor-medical", typeof(DevCoreHospital.Views.MedicalEvaluationView));
                    Add("Shift Swap", "doctor-shift-swap", typeof(ShiftSwapPlaceholderPage));
                    Add("See Schedule", "doctor-schedule", typeof(DoctorSchedulePage));
                    Add("Salary", "doctor-salary", typeof(DevCoreHospital.Views.SalaryPlaceholderPage));
                    Add("Hang Out", "doctor-hangout", typeof(HangOutPlaceholderPage));
                    break;
            }

            var first = _items.FirstOrDefault();
            if (first != null)
            {
                MenuList.SelectedItem = first;
                NavigateToKey(first.Key);
            }
        }

        private void Add(string title, string key, Type pageType)
        {
            if (!typeof(Page).IsAssignableFrom(pageType))
                throw new InvalidOperationException($"{pageType.FullName} is not a Page.");

            _items.Add(new MenuEntry { Key = key, Title = title });
            _routes[key] = pageType;
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuList.SelectedItem is not MenuEntry entry)
                return;

            NavigateToKey(entry.Key);
        }

        private void NavigateToKey(string key)
        {
            if (!_routes.TryGetValue(key, out var pageType))
                pageType = typeof(NotImplementedPlaceholderPage);

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