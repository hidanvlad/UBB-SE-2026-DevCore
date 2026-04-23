using System;
using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Admin;
using DevCoreHospital.ViewModels.Doctor;
using DevCoreHospital.ViewModels.Pharmacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DevCoreHospital
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        private Window? window;

        public App()
        {
            InitializeComponent();
            Services = ConfigureServices().BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            window = new MainWindow();
            window.Activate();
        }

        private static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            RegisterInfrastructure(services);
            RegisterRepositories(services);
            RegisterServices(services);
            RegisterViewModels(services);

            return services;
        }

        private static void RegisterInfrastructure(IServiceCollection services)
        {
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IDialogService, DialogService>();
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            // StaffRepository implements IStaffRepository, IShiftManagementStaffRepository, IPharmacyStaffRepository.
            // Registered once as a singleton concrete type; all three interfaces forward to the same instance.
            services.AddSingleton<StaffRepository>(_ => new StaffRepository(AppSettings.ConnectionString));
            services.AddSingleton<IStaffRepository>(sp => sp.GetRequiredService<StaffRepository>());
            services.AddSingleton<IShiftManagementStaffRepository>(sp => sp.GetRequiredService<StaffRepository>());
            services.AddSingleton<IPharmacyStaffRepository>(sp => sp.GetRequiredService<StaffRepository>());

            // ShiftRepository implements IShiftRepository, IShiftManagementShiftRepository, IPharmacyShiftRepository.
            services.AddSingleton<ShiftRepository>(sp =>
                new ShiftRepository(AppSettings.ConnectionString, sp.GetRequiredService<StaffRepository>()));
            services.AddSingleton<IShiftRepository>(sp => sp.GetRequiredService<ShiftRepository>());
            services.AddSingleton<IShiftManagementShiftRepository>(sp => sp.GetRequiredService<ShiftRepository>());
            services.AddSingleton<IPharmacyShiftRepository>(sp => sp.GetRequiredService<ShiftRepository>());

            services.AddSingleton<SalaryRepository>(_ => new SalaryRepository(AppSettings.ConnectionString));
            services.AddSingleton<ShiftSwapRepository>(_ => new ShiftSwapRepository(AppSettings.ConnectionString));
            services.AddSingleton<IShiftSwapRepository>(sp => sp.GetRequiredService<ShiftSwapRepository>());

            services.AddSingleton<AppointmentRepository>(_ => new AppointmentRepository(AppSettings.ConnectionString));
            services.AddSingleton<IDoctorAppointmentDataSource>(sp => sp.GetRequiredService<AppointmentRepository>());

            services.AddSingleton<IHangoutRepository, HangoutRepository>();

            services.AddSingleton<IEvaluationsRepository>(_ => new EvaluationsRepository(AppSettings.ConnectionString));

            // Data sources for ER dispatch and fatigue audit
            services.AddSingleton<IERDispatchDataSource>(_ => new SqlERDispatchDataSource(AppSettings.ConnectionString));
            services.AddSingleton<IFatigueShiftDataSource>(_ => new SqlFatigueShiftDataSource(AppSettings.ConnectionString));

            services.AddSingleton<IERDispatchRepository, ERDispatchRepository>();
            services.AddSingleton<IFatigueAuditRepository, FatigueAuditRepository>();
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IDoctorAppointmentService, DoctorAppointmentService>();
            services.AddSingleton<IERDispatchService, ERDispatchService>();
            services.AddSingleton<IFatigueAuditService, FatigueAuditService>();
            services.AddSingleton<IHangoutService, HangoutService>();
            services.AddSingleton<IPharmacyScheduleService, PharmacyScheduleService>();
            services.AddSingleton<IPharmacyVacationService, PharmacyVacationService>();
            services.AddSingleton<IShiftManagementService, ShiftManagementService>();
            services.AddSingleton<IShiftSwapService, ShiftSwapService>();
            services.AddSingleton<ISalaryComputationService, SalaryComputationService>();
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            // Single-constructor ViewModels: DI resolves automatically.
            services.AddTransient<AdminShiftViewModel>();
            services.AddTransient<AdminAppointmentsViewModel>();
            services.AddTransient<ERDispatchViewModel>();
            services.AddTransient<FatigueShiftAuditViewModel>();
            services.AddTransient<DoctorScheduleViewModel>();
            services.AddTransient<MyScheduleViewModel>();
            services.AddTransient<PharmacyScheduleViewModel>();
            services.AddTransient<PharmacistVacationViewModel>();
            services.AddTransient<MedicalEvaluationViewModel>();

            // Multi-constructor ViewModels: use explicit factories to avoid ambiguity.
            // MS DI treats IEnumerable<T> as always-resolvable (returns empty collection),
            // so any ViewModel with an IEnumerable<T> overload must be wired explicitly.
            services.AddTransient<IncomingSwapRequestsViewModel>(sp =>
                new IncomingSwapRequestsViewModel(
                    sp.GetRequiredService<IShiftSwapService>(),
                    sp.GetRequiredService<IStaffRepository>()));

            services.AddTransient<HangoutViewModel>(sp =>
                new HangoutViewModel(
                    sp.GetRequiredService<IHangoutService>(),
                    sp.GetRequiredService<IDoctorAppointmentService>()));

            services.AddTransient<SalaryComputationViewModel>(sp =>
                new SalaryComputationViewModel(
                    sp.GetRequiredService<ISalaryComputationService>(),
                    sp.GetRequiredService<IStaffRepository>(),
                    sp.GetRequiredService<IShiftManagementShiftRepository>()));
        }
    }
}
