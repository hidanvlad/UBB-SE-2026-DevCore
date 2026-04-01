-- 1. THE RESET SWITCH: Safely drop and recreate the entire database
USE master;
GO

-- Force close any background connections to the database so we can delete it
ALTER DATABASE HospitalDatabase SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE HospitalDatabase;
GO

-- Create a fresh, empty database
CREATE DATABASE HospitalDatabase;
GO

USE HospitalDatabase;
GO

-- ---------------------------------------------------------
-- 2. CREATE TABLES
-- ---------------------------------------------------------
CREATE TABLE Staff (
    staff_id INT PRIMARY KEY IDENTITY(1,1),
    [role] VARCHAR(255),
    department VARCHAR(255),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    contact_info VARCHAR(255),
    is_available BIT,
    license_number VARCHAR(100),
    specialization VARCHAR(100),
    [status] VARCHAR(100),
    certification VARCHAR(100),
    years_of_experience INT
);

CREATE TABLE Shifts (
    shift_id INT PRIMARY KEY IDENTITY(1,1),
    staff_id INT NOT NULL,
    [location] VARCHAR(100),
    start_time DATETIME,
    end_time DATETIME,
    [status] VARCHAR(50),
    is_active BIT,
    CONSTRAINT FK_Shifts_Staff FOREIGN KEY (staff_id) REFERENCES Staff(staff_id)
);

CREATE TABLE Appointments (
    appointment_id INT PRIMARY KEY IDENTITY(1,1),
    patient_id INT NOT NULL, 
    doctor_id INT NOT NULL,
    start_time DATETIME,
    end_time DATETIME,
    [status] VARCHAR(50),
    CONSTRAINT FK_Appointments_Doctor FOREIGN KEY (doctor_id) REFERENCES Staff(staff_id)
);

CREATE TABLE Medical_Evaluations (
    evaluation_id INT PRIMARY KEY IDENTITY(1,1),
    doctor_id INT NOT NULL,
    patient_id INT NOT NULL,
    diagnosis TEXT,           
    doctor_notes TEXT,       
    medications TEXT,        
    source VARCHAR(255),
    assumed_risk BIT,
    CONSTRAINT FK_Evaluations_Doctor FOREIGN KEY (doctor_id) REFERENCES Staff(staff_id)
);

CREATE TABLE Evaluation_Symptoms (
    evaluation_id INT NOT NULL,
    symptom_id INT NOT NULL,
    PRIMARY KEY (evaluation_id, symptom_id),
    CONSTRAINT FK_EvalSymp_Eval FOREIGN KEY (evaluation_id) REFERENCES Medical_Evaluations(evaluation_id)
);

CREATE TABLE Evaluation_Medications (
    evaluation_id INT NOT NULL,
    medication_id INT NOT NULL,
    PRIMARY KEY (evaluation_id, medication_id),
    CONSTRAINT FK_EvalMed_Eval FOREIGN KEY (evaluation_id) REFERENCES Medical_Evaluations(evaluation_id)
);

CREATE TABLE Hangouts (
    hangout_id INT PRIMARY KEY IDENTITY(1,1),
    title VARCHAR(25),
    description VARCHAR(100),
    date_time DATETIME,
    max_staff INT
);

CREATE TABLE Hangout_Participants (
    hangout_id INT NOT NULL,
    staff_id INT NOT NULL,
    PRIMARY KEY (hangout_id, staff_id),
    CONSTRAINT FK_HangoutPart_Hangout FOREIGN KEY (hangout_id) REFERENCES Hangouts(hangout_id),
    CONSTRAINT FK_HangoutPart_Staff FOREIGN KEY (staff_id) REFERENCES Staff(staff_id)
);

CREATE TABLE High_Risk_Medicines (
    medicine_id INT PRIMARY KEY IDENTITY(1,1),
    medicine_name VARCHAR(100) NOT NULL,
    warning_message VARCHAR(255) NOT NULL
);

-- SHIFT SWAP FEATURE TABLES
CREATE TABLE ShiftSwapRequests (
    swap_id INT IDENTITY(1,1) PRIMARY KEY,
    shift_id INT NOT NULL,
    requester_id INT NOT NULL,
    colleague_id INT NOT NULL,
    requested_at DATETIME NOT NULL DEFAULT GETUTCDATE(),
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    CONSTRAINT FK_SwapReq_Shift FOREIGN KEY (shift_id) REFERENCES Shifts(shift_id),
    CONSTRAINT FK_SwapReq_Requester FOREIGN KEY (requester_id) REFERENCES Staff(staff_id),
    CONSTRAINT FK_SwapReq_Colleague FOREIGN KEY (colleague_id) REFERENCES Staff(staff_id)
);

CREATE TABLE Notifications (
    notification_id INT IDENTITY(1,1) PRIMARY KEY,
    recipient_staff_id INT NOT NULL,
    title VARCHAR(200) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT GETUTCDATE(),
    is_read BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Notif_Staff FOREIGN KEY (recipient_staff_id) REFERENCES Staff(staff_id)
);

-- ER DISPATCH TABLES
CREATE TABLE ER_Requests (
    request_id INT IDENTITY(101,1) PRIMARY KEY,
    specialization VARCHAR(100) NOT NULL,
    [location] VARCHAR(100) NOT NULL,
    created_at DATETIME NOT NULL CONSTRAINT DF_ER_Requests_created_at DEFAULT GETDATE(),
    [status] VARCHAR(50) NOT NULL,
    assigned_doctor_id INT NULL,
    assigned_doctor_name VARCHAR(200) NULL,
    CONSTRAINT CK_ER_Requests_status CHECK (LOWER([status]) IN ('pending','assigned','unmatched','completed')),
    CONSTRAINT FK_ER_Requests_staff FOREIGN KEY (assigned_doctor_id) REFERENCES Staff(staff_id)
);
GO

-- ---------------------------------------------------------
-- 3. INSERT STATIC TEST DATA
-- ---------------------------------------------------------

-- Insert High Risk Medicines
INSERT INTO High_Risk_Medicines (medicine_name, warning_message)
VALUES 
('Warfarin', 'Blood thinner conflict: High risk of internal bleeding.'),
('Insulin', 'Glucose conflict: Requires immediate sugar level monitoring.'),
('Penicillin', 'Allergy Warning: History of anaphylaxis in this department.');

-- Insert Staff (Combined existing staff, shift swap tests, and ER/Fatigue Audit tests)
INSERT INTO Staff ([role], department, first_name, last_name, is_available, specialization, status, contact_info, license_number, years_of_experience, certification)
VALUES 
-- Original Doctors
('Doctor', 'Cardiology', 'John', 'Smith', 1, 'Cardiologist', 'Available', 'info1', 'A1234', 20, NULL),
('Doctor', 'Cardiology', 'Alice', 'Jones', 1, 'Cardiologist', 'Available', 'info2', 'B4321', 15, NULL),
('Doctor', 'Diagnostic Medicine', 'Gregory', 'House', 1, 'Diagnostician', 'Available', 'info4', 'C9876', 25, NULL),
('Doctor', 'Oncology', 'James', 'Wilson', 1, 'Oncologist', 'Available', 'info5', 'D5555', 20, NULL),
('Doctor', 'Endocrinology', 'Lisa', 'Cuddy', 0, 'Endocrinologist', 'Off_Duty', 'info6', 'E7777', 18, NULL),
('Doctor', 'Cardiology', 'Mihai', 'Popescu', 1, 'Cardiologist', 'Off_Duty', 'info_mihai', 'DOC-1003', 8, NULL),
('Doctor', 'Emergency', 'Andreea', 'Ionescu', 1, 'Emergency', 'In_Examination', 'info_andreea', 'DOC-1004', 11, NULL),
-- ER/Fatigue Audit Doctors
('Doctor', 'Cardiology', 'Michael', 'Scott', 1, 'Cardiologist', 'Available', 'michael.scott@local', 'Cardio-001', 8, NULL),
('Doctor', 'Cardiology', 'Sarah', 'Jenkins', 1, 'Cardiologist', 'Available', 'sarah.jenkins@local', 'Cardio-002', 5, NULL),
('Doctor', 'Cardiology', 'David', 'Bradley', 1, 'Cardiologist', 'Available', 'david.bradley@local', 'Cardio-003', 12, NULL),
('Doctor', 'Emergency', 'Emma', 'Thompson', 1, 'Surgeon', 'Available', 'emma.thompson@local', 'Surg-001', 9, NULL),
('Doctor', 'Emergency', 'James', 'Wilson_ER', 0, 'Surgeon', 'In_Examination', 'james.wilson.er@local', 'Surg-002', 11, NULL),
-- Pharmacists
('Pharmacist', 'Pharmacy', 'Robert', 'White', 0, NULL, 'Off_Duty', 'info3', NULL, 13, 'BPS'),
('Pharmacist', 'Pharmacy', 'Jane', 'Doe', 1, NULL, 'Available', 'info7', NULL, 8, 'PharmD'),
('Pharmacist', 'Pharmacy', 'Mark', 'Spencer', 1, NULL, 'Available', 'info8', NULL, 5, 'BCPS'),
('Pharmacist', 'Pharmacy', 'Elena', 'Radu', 1, NULL, 'Available', 'info_elena', NULL, 7, 'Clinical');

-- Insert Appointments
INSERT INTO Appointments (patient_id, doctor_id, start_time, [status])
VALUES (7759376, 1, GETDATE(), 'Confirmed');
INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status)
VALUES (500, 1, '2026-04-05 10:30:00', '2026-04-05 11:30:00', 'Confirmed');
INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status)
VALUES (501, 3, '2026-04-06 09:00:00', '2026-04-06 10:00:00', 'Scheduled');
INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status)
VALUES (502, 1, '2026-04-10 14:00:00', '2026-04-10 15:00:00', 'Scheduled');
INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status)
VALUES (503, 2, '2026-04-15 10:00:00', '2026-04-15 11:00:00', 'Scheduled');
INSERT INTO Appointments (patient_id, doctor_id, start_time, [status])
VALUES 
(7759376, 1, GETDATE(), 'Confirmed'), 
(503, 2, GETDATE(), 'Confirmed'),     
(500, 3, GETDATE(), 'Confirmed');     

-- Insert Historical Shifts (Mix of completed and scheduled)
INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
VALUES 
(1, 'Cardiology Wing', '2026-03-25 08:00:00', '2026-03-25 16:00:00', 'Completed', 0),
(2, 'Ward A', '2026-04-01 08:00:00', '2026-04-01 15:00:00', 'Completed', 1),
(3, 'ER', '2026-04-02 09:00:00', '2026-04-02 22:00:00', 'Scheduled', 1),
(3, 'Clinic', '2026-04-02 09:00:00', '2026-04-02 23:00:00', 'Scheduled', 1),
(3, 'ICU', '2026-04-03 08:00:00', '2026-04-03 20:00:00', 'Scheduled', 1),
(4, 'Oncology Wing', '2026-04-01 09:00:00', '2026-04-01 17:00:00', 'Completed', 1),
(5, 'Cardiology Wing', '2026-04-10 08:00:00', '2026-04-10 16:00:00', 'Scheduled', 1),
(6, 'ER', '2026-04-15 08:00:00', '2026-04-15 16:00:00', 'Scheduled', 1),
(7, 'Clinic', '2026-04-10 09:00:00', '2026-04-10 17:00:00', 'Scheduled', 1),
(8, 'Main Pharmacy', '2026-04-01 08:00:00', '2026-04-01 16:00:00', 'Completed', 1),
(9, 'ER Pharmacy', '2026-04-02 16:00:00', '2026-04-03 00:00:00', 'Scheduled', 1),
(9, 'Main Pharmacy', '2026-04-03 08:00:00', '2026-04-03 16:00:00', 'Scheduled', 1),
(10, 'Main Pharmacy', '2026-04-01 16:00:00', '2026-04-02 00:00:00', 'Completed', 1);


-- Insert Medical Evaluations 
INSERT INTO Medical_Evaluations (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
VALUES 

(1, 7759376, 'Severe Penicillin Allergy', 'Hives reported.', 'Penicillin', 'Historical', 0),
(2, 503, 'Previous Adverse Reactions', 'Nausea with Aspirin.', 'Aspirin', 'Historical', 0);

-- Insert Hangouts
INSERT INTO Hangouts (title, description, date_time, max_staff)
VALUES 
('Friday Pizza', 'Weekly team bonding in the breakroom', '2026-04-03 17:00:00', 10),
('Coffee Break', 'Quick catchup before morning rounds', '2026-04-02 07:30:00', 5),
('Future Movie Night', 'Watching a medical drama', '2026-04-10 19:00:00', 10),
('Mid-April Lunch', 'Lunch outing', '2026-04-15 12:30:00', 8);

-- Insert Hangout Participants
INSERT INTO Hangout_Participants (hangout_id, staff_id)
VALUES (1, 1), (1, 2), (1, 8), (1, 9), (2, 3), (2, 4);
GO


-- ---------------------------------------------------------
-- 4. SETUP DYNAMIC TEST DATA (Shift Swapping & ER Dispatch)
-- ---------------------------------------------------------

-- 4A. SHIFT SWAPPING DYNAMIC SETUP
DECLARE @John INT      = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='John' AND last_name='Smith');
DECLARE @Alice INT     = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='Alice' AND last_name='Jones');
DECLARE @Mihai INT     = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='Mihai' AND last_name='Popescu');
DECLARE @Andreea INT   = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='Andreea' AND last_name='Ionescu');
DECLARE @Robert INT    = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='Robert' AND last_name='White');
DECLARE @Elena INT     = (SELECT TOP 1 staff_id FROM Staff WHERE first_name='Elena' AND last_name='Radu');

DECLARE @Now DATETIME = GETDATE();

INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
VALUES 
(@John, 'Cardio Ward A', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Alice, 'Cardio Ward B', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Mihai, 'Cardio Ward C', DATEADD(HOUR, 40, @Now), DATEADD(HOUR, 48, @Now), 'SCHEDULED', 1),
(@Andreea, 'ER Room 1', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'ACTIVE', 1),
(@Robert, 'Main Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Elena, 'Main Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1);

DECLARE @JohnShift INT = (SELECT TOP 1 shift_id FROM Shifts WHERE staff_id = @John AND start_time > @Now ORDER BY start_time);
DECLARE @RobertShift INT = (SELECT TOP 1 shift_id FROM Shifts WHERE staff_id = @Robert AND start_time > @Now ORDER BY start_time);

INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
VALUES 
(@JohnShift, @John, @Alice, DATEADD(MINUTE,-30,GETUTCDATE()), 'PENDING'),
(@JohnShift, @John, @Mihai, DATEADD(MINUTE,-25,GETUTCDATE()), 'PENDING'),
(@RobertShift, @Robert, @Elena, DATEADD(MINUTE,-20,GETUTCDATE()), 'PENDING');

INSERT INTO Notifications (recipient_staff_id, title, message, created_at, is_read)
VALUES
(@Alice, 'New Shift Swap Request', 'John Smith requested a swap for his upcoming Cardiology shift.', GETUTCDATE(), 0),
(@Elena, 'New Shift Swap Request', 'Robert White requested a pharmacy shift swap.', DATEADD(MINUTE,-10,GETUTCDATE()), 0),
(@John,  'Reminder', 'Check your swap requests status from Incoming Swap Requests.', DATEADD(HOUR,-1,GETUTCDATE()), 1);

-- 4B. FATIGUE AUDIT & ER DISPATCH DYNAMIC SETUP
DECLARE @Req3OverloadId INT = (SELECT TOP 1 staff_id FROM dbo.Staff WHERE first_name = 'Michael' AND last_name = 'Scott' ORDER BY staff_id DESC);
DECLARE @Req3LowId INT = (SELECT TOP 1 staff_id FROM dbo.Staff WHERE first_name = 'Sarah' AND last_name = 'Jenkins' ORDER BY staff_id DESC);
DECLARE @Req3HighId INT = (SELECT TOP 1 staff_id FROM dbo.Staff WHERE first_name = 'David' AND last_name = 'Bradley' ORDER BY staff_id DESC);
DECLARE @Req4AvailableId INT = (SELECT TOP 1 staff_id FROM dbo.Staff WHERE first_name = 'Emma' AND last_name = 'Thompson' ORDER BY staff_id DESC);
DECLARE @Req4BusyId INT = (SELECT TOP 1 staff_id FROM dbo.Staff WHERE first_name = 'James' AND last_name = 'Wilson_ER' ORDER BY staff_id DESC);

INSERT INTO dbo.Shifts (staff_id, [location], start_time, end_time, [status], is_active)
VALUES
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

INSERT INTO dbo.ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
VALUES
('Surgeon', 'Ward A', '2026-03-30 09:45:00', 'Pending', null, null),
('Cardiologist', 'Ward A', '2026-03-30 09:50:00', 'Pending', null, null),
('Neurology', 'Ward A', '2026-03-30 09:55:00', 'Pending', null, null),
('Surgeon', 'Ward A', '2026-03-30 09:59:00', 'Pending', null, null),
('Pediatrics', 'Ward A', '2026-03-30 10:00:00', 'Pending', null, null);
GO

-- ---------------------------------------------------------
-- 5. VALIDATION OUTPUT
-- ---------------------------------------------------------
use HospitalDatabase;
USE HospitalDatabase;
GO

SELECT * FROM Staff;
SELECT * FROM Shifts;
SELECT * FROM ShiftSwapRequests;
SELECT * FROM Notifications;
SELECT * FROM ER_Requests;
