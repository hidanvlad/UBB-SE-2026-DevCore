using System;
using DevCoreHospital.Configuration;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Admin;
using DevCoreHospital.ViewModels.Doctor;
using DevCoreHospital.ViewModels.Pharmacy;
using DevCoreHospital.Views.Shell;
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

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs eventArgs)
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
            services.AddSingleton<DialogPresenter>();
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            // StaffRepository implements IStaffRepository, IShiftManagementStaffRepository, IPharmacyStaffRepository.
            // Registered once as a singleton concrete type; all three interfaces forward to the same instance.
            static StaffRepository CreateStaffRepository(IServiceProvider serviceProvider) => new StaffRepository(AppSettings.ConnectionString);
            services.AddSingleton<StaffRepository>(CreateStaffRepository);
            static StaffRepository ResolveStaffRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<StaffRepository>();
            services.AddSingleton<IStaffRepository>(ResolveStaffRepository);
            static StaffRepository ResolveShiftManagementStaffRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<StaffRepository>();
            services.AddSingleton<IShiftManagementStaffRepository>(ResolveShiftManagementStaffRepository);
            static StaffRepository ResolvePharmacyStaffRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<StaffRepository>();
            services.AddSingleton<IPharmacyStaffRepository>(ResolvePharmacyStaffRepository);

            // ShiftRepository implements IShiftRepository, IShiftManagementShiftRepository, IPharmacyShiftRepository.
            static ShiftRepository CreateShiftRepository(IServiceProvider serviceProvider) =>
                new ShiftRepository(AppSettings.ConnectionString);
            services.AddSingleton<ShiftRepository>(CreateShiftRepository);
            static ShiftRepository ResolveShiftRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<ShiftRepository>();
            services.AddSingleton<IShiftRepository>(ResolveShiftRepository);
            static ShiftRepository ResolveShiftManagementShiftRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<ShiftRepository>();
            services.AddSingleton<IShiftManagementShiftRepository>(ResolveShiftManagementShiftRepository);
            static ShiftRepository ResolvePharmacyShiftRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<ShiftRepository>();
            services.AddSingleton<IPharmacyShiftRepository>(ResolvePharmacyShiftRepository);

            static IPharmacyHandoverRepository CreatePharmacyHandoverRepository(IServiceProvider serviceProvider) =>
                new PharmacyHandoverRepository(AppSettings.ConnectionString);
            services.AddSingleton<IPharmacyHandoverRepository>(CreatePharmacyHandoverRepository);

            static IShiftSwapRepository CreateShiftSwapRepository(IServiceProvider serviceProvider) =>
                new ShiftSwapRepository(AppSettings.ConnectionString);
            services.AddSingleton<IShiftSwapRepository>(CreateShiftSwapRepository);

            static INotificationRepository CreateNotificationRepository(IServiceProvider serviceProvider) =>
                new NotificationRepository(AppSettings.ConnectionString);
            services.AddSingleton<INotificationRepository>(CreateNotificationRepository);

            static AppointmentRepository CreateAppointmentRepository(IServiceProvider serviceProvider) => new AppointmentRepository(AppSettings.ConnectionString);
            services.AddSingleton<AppointmentRepository>(CreateAppointmentRepository);
            static AppointmentRepository ResolveAppointmentRepository(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<AppointmentRepository>();
            services.AddSingleton<IAppointmentRepository>(ResolveAppointmentRepository);

            services.AddSingleton<IHangoutRepository, HangoutRepository>();
            static IHangoutParticipantRepository CreateHangoutParticipantRepository(IServiceProvider serviceProvider) =>
                new HangoutParticipantRepository(AppSettings.ConnectionString);
            services.AddSingleton<IHangoutParticipantRepository>(CreateHangoutParticipantRepository);

            static IEvaluationsRepository CreateEvaluationsRepository(IServiceProvider serviceProvider) => new EvaluationsRepository(AppSettings.ConnectionString);
            services.AddSingleton<IEvaluationsRepository>(CreateEvaluationsRepository);

            static IERDispatchRepository CreateERDispatchRepository(IServiceProvider serviceProvider) => new ERDispatchRepository(AppSettings.ConnectionString);
            services.AddSingleton<IERDispatchRepository>(CreateERDispatchRepository);

            static IHighRiskMedicineRepository CreateHighRiskMedicineRepository(IServiceProvider serviceProvider) =>
                new HighRiskMedicineRepository(AppSettings.ConnectionString);
            services.AddSingleton<IHighRiskMedicineRepository>(CreateHighRiskMedicineRepository);
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
            static ISalaryComputationService CreateSalaryComputationService(IServiceProvider serviceProvider) =>
                new SalaryComputationService(
                    serviceProvider.GetRequiredService<IPharmacyHandoverRepository>(),
                    serviceProvider.GetRequiredService<IHangoutRepository>(),
                    serviceProvider.GetRequiredService<IHangoutParticipantRepository>(),
                    serviceProvider.GetRequiredService<IStaffRepository>(),
                    serviceProvider.GetRequiredService<IShiftManagementShiftRepository>());
            services.AddSingleton<ISalaryComputationService>(CreateSalaryComputationService);
            services.AddSingleton<IMedicalEvaluationService, MedicalEvaluationService>();
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
            static IncomingSwapRequestsViewModel CreateIncomingSwapRequestsViewModel(IServiceProvider serviceProvider) =>
                new IncomingSwapRequestsViewModel(
                    serviceProvider.GetRequiredService<IShiftSwapService>());
            services.AddTransient<IncomingSwapRequestsViewModel>(CreateIncomingSwapRequestsViewModel);

            static HangoutViewModel CreateHangoutViewModel(IServiceProvider serviceProvider) =>
                new HangoutViewModel(
                    serviceProvider.GetRequiredService<IHangoutService>(),
                    serviceProvider.GetRequiredService<IDoctorAppointmentService>());
            services.AddTransient<HangoutViewModel>(CreateHangoutViewModel);

            static SalaryComputationViewModel CreateSalaryComputationViewModel(IServiceProvider serviceProvider) =>
                new SalaryComputationViewModel(
                    serviceProvider.GetRequiredService<ISalaryComputationService>());
            services.AddTransient<SalaryComputationViewModel>(CreateSalaryComputationViewModel);
        }
    }
}
