use [HospitalDatabase];

begin try
    if object_id(N'dbo.ER_Requests', N'U') is null
    begin
        create table dbo.ER_Requests (
            request_id int identity(101,1) primary key,
            specialization varchar(100) not null,
            [location] varchar(100) not null,
            created_at datetime not null constraint DF_ER_Requests_created_at default getdate(),
            [status] varchar(50) not null,
            assigned_doctor_id int null,
            assigned_doctor_name varchar(200) null,
            constraint CK_ER_Requests_status check (lower([status]) in ('pending','assigned','unmatched','completed')),
            constraint FK_ER_Requests_staff foreign key (assigned_doctor_id) references dbo.Staff(staff_id)
        );
    end
end try
begin catch
    throw;
end catch;

insert into dbo.Staff ([role], department, first_name, last_name, contact_info, is_available, specialization, [status], license_number, years_of_experience)
values
('Doctor', 'Cardiology', 'Michael', 'Scott', 'michael.scott@local', 1, 'Cardiologist', 'Available', 'Cardio-001', 8),
('Doctor', 'Cardiology', 'Sarah', 'Jenkins', 'sarah.jenkins@local', 1, 'Cardiologist', 'Available', 'Cardio-002', 5),
('Doctor', 'Cardiology', 'David', 'Bradley', 'david.bradley@local', 1, 'Cardiologist', 'Available', 'Cardio-003', 12),
('Doctor', 'Emergency', 'Emma', 'Thompson', 'emma.thompson@local', 1, 'Surgeon', 'Available', 'Surg-001', 9),
('Doctor', 'Emergency', 'James', 'Wilson', 'james.wilson@local', 0, 'Surgeon', 'In_Examination', 'Surg-002', 11);

-- Căutăm ID-urile folosind noile nume normale pentru a le lega de ture
declare @Req3OverloadId int = (select top 1 staff_id from dbo.Staff where first_name = 'Michael' and last_name = 'Scott' order by staff_id desc);
declare @Req3LowId int = (select top 1 staff_id from dbo.Staff where first_name = 'Sarah' and last_name = 'Jenkins' order by staff_id desc);
declare @Req3HighId int = (select top 1 staff_id from dbo.Staff where first_name = 'David' and last_name = 'Bradley' order by staff_id desc);
declare @Req4AvailableId int = (select top 1 staff_id from dbo.Staff where first_name = 'Emma' and last_name = 'Thompson' order by staff_id desc);
declare @Req4BusyId int = (select top 1 staff_id from dbo.Staff where first_name = 'James' and last_name = 'Wilson' order by staff_id desc);

insert into dbo.Shifts (staff_id, [location], start_time, end_time, [status], is_active)
values
(@Req3OverloadId, 'Ward A', '2026-03-30 08:00:00', '2026-03-30 20:00:00', 'Scheduled', 0),
(@Req3OverloadId, 'Ward A', '2026-03-31 06:00:00', '2026-03-31 18:00:00', 'Scheduled', 0),
(@Req3OverloadId, 'Ward A', '2026-04-01 08:00:00', '2026-04-01 20:00:00', 'Scheduled', 0),
(@Req3OverloadId, 'Ward A', '2026-04-02 08:00:00', '2026-04-02 20:00:00', 'Scheduled', 0),
(@Req3OverloadId, 'Ward A', '2026-04-03 08:00:00', '2026-04-03 20:00:00', 'Scheduled', 0),
(@Req3OverloadId, 'Ward A', '2026-04-04 08:00:00', '2026-04-04 14:00:00', 'Scheduled', 0),
(@Req3LowId, 'Ward B', '2026-03-20 12:00:00', '2026-03-20 16:00:00', 'Completed', 0),
(@Req3HighId, 'Ward B', '2026-03-18 08:00:00', '2026-03-18 16:00:00', 'Completed', 0),
(@Req3HighId, 'Ward B', '2026-03-22 08:00:00', '2026-03-22 16:00:00', 'Completed', 0),
(@Req3HighId, 'Ward B', '2026-03-26 08:00:00', '2026-03-26 16:00:00', 'Completed', 0),
(@Req4AvailableId, 'Ward A', '2026-03-30 06:00:00', '2026-03-30 18:00:00', 'Active', 1),
(@Req4BusyId, 'Ward A', '2026-03-30 07:00:00', '2026-03-30 15:00:00', 'Active', 1);

insert into dbo.ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
values
('Surgeon', 'Ward A', '2026-03-30 09:45:00', 'Pending', null, null),
('Cardiologist', 'Ward A', '2026-03-30 09:50:00', 'Pending', null, null),
('Neurology', 'Ward A', '2026-03-30 09:55:00', 'Pending', null, null),
('Surgeon', 'Ward A', '2026-03-30 09:59:00', 'Pending', null, null),
('Pediatrics', 'Ward A', '2026-03-30 10:00:00', 'Pending', null, null);