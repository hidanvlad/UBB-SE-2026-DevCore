USE master;
GO

IF DB_ID('HospitalDatabase') IS NOT NULL
BEGIN
    ALTER DATABASE HospitalDatabase SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE HospitalDatabase;
END
GO

CREATE DATABASE HospitalDatabase;
GO

USE HospitalDatabase;
GO

/* =========================
   TABLES
   ========================= */
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

CREATE TABLE ShiftSwapRequests (
    swap_id INT IDENTITY(1,1) PRIMARY KEY,
    shift_id INT NOT NULL,
    requester_id INT NOT NULL,
    colleague_id INT NOT NULL,
    requested_at DATETIME NOT NULL DEFAULT GETUTCDATE(),
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    CONSTRAINT FK_SwapReq_Shift FOREIGN KEY (shift_id) REFERENCES Shifts(shift_id),
    CONSTRAINT FK_SwapReq_Requester FOREIGN KEY (requester_id) REFERENCES Staff(staff_id),
    CONSTRAINT FK_SwapReq_Colleague FOREIGN KEY (colleague_id) REFERENCES Staff(staff_id),
    CONSTRAINT CK_SwapReq_Status CHECK (status IN ('PENDING','ACCEPTED','REJECTED','CANCELLED')),
    CONSTRAINT CK_SwapReq_DifferentUsers CHECK (requester_id <> colleague_id)
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

CREATE INDEX IX_Shifts_Staff_Time ON Shifts(staff_id, start_time, end_time);
CREATE INDEX IX_ShiftSwap_Colleague_Status ON ShiftSwapRequests(colleague_id, status, requested_at DESC);
CREATE INDEX IX_ShiftSwap_Requester_Status ON ShiftSwapRequests(requester_id, status, requested_at DESC);
CREATE INDEX IX_Notifications_Recipient_Created ON Notifications(recipient_staff_id, created_at DESC);
GO

/* =========================
   BASE DATA
   ========================= */
INSERT INTO High_Risk_Medicines (medicine_name, warning_message)
VALUES
('Warfarin', 'Blood thinner conflict: High risk of internal bleeding.'),
('Insulin', 'Glucose conflict: Requires immediate sugar level monitoring.'),
('Penicillin', 'Allergy Warning: History of anaphylaxis in this department.');

INSERT INTO Staff ([role], department, first_name, last_name, is_available, specialization, status, contact_info, license_number, years_of_experience, certification)
VALUES
('Doctor', 'Cardiology', 'John', 'Smith', 1, 'Cardiologist', 'Available', 'john.smith@hospital.local', 'DOC-1001', 20, NULL),
('Doctor', 'Cardiology', 'Alice', 'Jones', 1, 'Cardiologist', 'Available', 'alice.jones@hospital.local', 'DOC-1002', 15, NULL),
('Doctor', 'Cardiology', 'Mihai', 'Popescu', 1, 'Cardiologist', 'Off_Duty', 'mihai.popescu@hospital.local', 'DOC-1003', 8, NULL),
('Doctor', 'Cardiology', 'Michael', 'Scott', 1, 'Cardiologist', 'Available', 'michael.scott@hospital.local', 'Cardio-001', 8, NULL),
('Doctor', 'Cardiology', 'Sarah', 'Jenkins', 1, 'Cardiologist', 'Available', 'sarah.jenkins@hospital.local', 'Cardio-002', 5, NULL),
('Doctor', 'Cardiology', 'David', 'Bradley', 1, 'Cardiologist', 'Available', 'david.bradley@hospital.local', 'Cardio-003', 12, NULL),
('Doctor', 'Emergency', 'Andreea', 'Ionescu', 1, 'Emergency', 'In_Examination', 'andreea.ionescu@hospital.local', 'DOC-1004', 11, NULL),
('Doctor', 'Emergency', 'Emma', 'Thompson', 1, 'Surgeon', 'Available', 'emma.thompson@hospital.local', 'Surg-001', 9, NULL),
('Doctor', 'Emergency', 'James', 'Wilson_ER', 0, 'Surgeon', 'In_Examination', 'james.wilson.er@hospital.local', 'Surg-002', 11, NULL),
('Doctor', 'Diagnostic Medicine', 'Gregory', 'House', 1, 'Diagnostician', 'Available', 'greg.house@hospital.local', 'C9876', 25, NULL),
('Doctor', 'Oncology', 'James', 'Wilson', 1, 'Oncologist', 'Available', 'james.wilson@hospital.local', 'D5555', 20, NULL),
('Doctor', 'Endocrinology', 'Lisa', 'Cuddy', 0, 'Endocrinologist', 'Off_Duty', 'lisa.cuddy@hospital.local', 'E7777', 18, NULL),
('Pharmacist', 'Pharmacy', 'Robert', 'White', 0, NULL, 'Off_Duty', 'robert.white@hospital.local', NULL, 13, 'BPS'),
('Pharmacist', 'Pharmacy', 'Jane', 'Doe', 1, NULL, 'Available', 'jane.doe@hospital.local', NULL, 8, 'PharmD'),
('Pharmacist', 'Pharmacy', 'Mark', 'Spencer', 1, NULL, 'Available', 'mark.spencer@hospital.local', NULL, 5, 'BCPS'),
('Pharmacist', 'Pharmacy', 'Elena', 'Radu', 1, NULL, 'Available', 'elena.radu@hospital.local', NULL, 7, 'Clinical'),
('Pharmacist', 'Pharmacy', 'Victor', 'Marin', 1, NULL, 'Available', 'victor.marin@hospital.local', NULL, 6, 'Clinical'),
('Pharmacist', 'Pharmacy', 'Ana', 'Pop', 1, NULL, 'Available', 'ana.pop@hospital.local', NULL, 9, 'PharmD');
GO

/* =========================
   OTHER DOMAIN DATA
   ========================= */
INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, [status])
VALUES
(7759376, 1, GETDATE(), DATEADD(MINUTE, 45, GETDATE()), 'Confirmed'),
(500, 1, '2026-04-05 10:30:00', '2026-04-05 11:30:00', 'Confirmed'),
(501, 10, '2026-04-06 09:00:00', '2026-04-06 10:00:00', 'Scheduled'),
(502, 1, '2026-04-10 14:00:00', '2026-04-10 15:00:00', 'Scheduled'),
(503, 2, '2026-04-15 10:00:00', '2026-04-15 11:00:00', 'Scheduled');

INSERT INTO Medical_Evaluations (doctor_id, patient_id, diagnosis, doctor_notes, medications, source, assumed_risk)
VALUES
(1, 7759376, 'Severe Penicillin Allergy', 'Hives reported.', 'Penicillin', 'Historical', 0),
(2, 503, 'Previous Adverse Reactions', 'Nausea with Aspirin.', 'Aspirin', 'Historical', 0);

INSERT INTO Hangouts (title, description, date_time, max_staff)
VALUES
('Friday Pizza', 'Weekly team bonding in the breakroom', '2026-04-03 17:00:00', 10),
('Coffee Break', 'Quick catchup before morning rounds', '2026-04-02 07:30:00', 5),
('Movie Night', 'Watching a medical drama', '2026-04-10 19:00:00', 10),
('Mid-April Lunch', 'Lunch outing', '2026-04-15 12:30:00', 8);

INSERT INTO Hangout_Participants (hangout_id, staff_id)
VALUES
(1, 1), (1, 2), (1, 13), (1, 16),
(2, 3), (2, 4),
(3, 8), (3, 10), (3, 14),
(4, 5), (4, 6), (4, 11);
GO

/* =========================
   SHIFTS DATA
   ========================= */
DECLARE @Now DATETIME = GETDATE();

DECLARE @John INT      = (SELECT staff_id FROM Staff WHERE first_name='John' AND last_name='Smith');
DECLARE @Alice INT     = (SELECT staff_id FROM Staff WHERE first_name='Alice' AND last_name='Jones');
DECLARE @Mihai INT     = (SELECT staff_id FROM Staff WHERE first_name='Mihai' AND last_name='Popescu');
DECLARE @Michael INT   = (SELECT staff_id FROM Staff WHERE first_name='Michael' AND last_name='Scott');
DECLARE @Sarah INT     = (SELECT staff_id FROM Staff WHERE first_name='Sarah' AND last_name='Jenkins');
DECLARE @David INT     = (SELECT staff_id FROM Staff WHERE first_name='David' AND last_name='Bradley');
DECLARE @Andreea INT   = (SELECT staff_id FROM Staff WHERE first_name='Andreea' AND last_name='Ionescu');
DECLARE @Emma INT      = (SELECT staff_id FROM Staff WHERE first_name='Emma' AND last_name='Thompson');
DECLARE @JamesER INT   = (SELECT staff_id FROM Staff WHERE first_name='James' AND last_name='Wilson_ER');
DECLARE @Robert INT    = (SELECT staff_id FROM Staff WHERE first_name='Robert' AND last_name='White');
DECLARE @Jane INT      = (SELECT staff_id FROM Staff WHERE first_name='Jane' AND last_name='Doe');
DECLARE @Mark INT      = (SELECT staff_id FROM Staff WHERE first_name='Mark' AND last_name='Spencer');
DECLARE @Elena INT     = (SELECT staff_id FROM Staff WHERE first_name='Elena' AND last_name='Radu');
DECLARE @Victor INT    = (SELECT staff_id FROM Staff WHERE first_name='Victor' AND last_name='Marin');
DECLARE @Ana INT       = (SELECT staff_id FROM Staff WHERE first_name='Ana' AND last_name='Pop');

INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
VALUES
(@John, 'Cardio Ward A', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@John, 'Cardio Ward A', DATEADD(HOUR, 72, @Now), DATEADD(HOUR, 80, @Now), 'SCHEDULED', 1),
(@John, 'Cardio Ward A', DATEADD(HOUR,-72, @Now), DATEADD(HOUR,-64, @Now), 'COMPLETED', 0),
(@Alice, 'Cardio Ward B', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Alice, 'Cardio Ward B', DATEADD(HOUR, 72, @Now), DATEADD(HOUR, 80, @Now), 'ACTIVE', 1),
(@Mihai, 'Cardio Ward C', DATEADD(HOUR, 40, @Now), DATEADD(HOUR, 48, @Now), 'SCHEDULED', 1),
(@Michael, 'Cardio Ward C', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Sarah, 'Cardio Ward D', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@David, 'Cardio Ward E', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'ACTIVE', 1),
(@Andreea, 'ER Room 1', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'ACTIVE', 1),
(@Emma, 'ER Room 2', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@JamesER, 'ER Room 3', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 30, @Now), 'ACTIVE', 1),
(@Robert, 'Main Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Elena, 'Main Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Victor, 'Main Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'ACTIVE', 1),
(@Jane, 'ER Pharmacy', DATEADD(HOUR, 24, @Now), DATEADD(HOUR, 32, @Now), 'SCHEDULED', 1),
(@Mark, 'ER Pharmacy', DATEADD(HOUR, 30, @Now), DATEADD(HOUR, 38, @Now), 'SCHEDULED', 1),
(@Ana, 'Main Pharmacy', DATEADD(HOUR, 48, @Now), DATEADD(HOUR, 56, @Now), 'SCHEDULED', 1);
GO

/* =========================
   SWAP REQUESTS DATA
   ========================= */
DECLARE @John INT      = (SELECT staff_id FROM Staff WHERE first_name='John' AND last_name='Smith');
DECLARE @Alice INT     = (SELECT staff_id FROM Staff WHERE first_name='Alice' AND last_name='Jones');
DECLARE @Mihai INT     = (SELECT staff_id FROM Staff WHERE first_name='Mihai' AND last_name='Popescu');
DECLARE @Michael INT   = (SELECT staff_id FROM Staff WHERE first_name='Michael' AND last_name='Scott');
DECLARE @Sarah INT     = (SELECT staff_id FROM Staff WHERE first_name='Sarah' AND last_name='Jenkins');
DECLARE @David INT     = (SELECT staff_id FROM Staff WHERE first_name='David' AND last_name='Bradley');
DECLARE @Robert INT    = (SELECT staff_id FROM Staff WHERE first_name='Robert' AND last_name='White');
DECLARE @Elena INT     = (SELECT staff_id FROM Staff WHERE first_name='Elena' AND last_name='Radu');
DECLARE @Victor INT    = (SELECT staff_id FROM Staff WHERE first_name='Victor' AND last_name='Marin');
DECLARE @Jane INT      = (SELECT staff_id FROM Staff WHERE first_name='Jane' AND last_name='Doe');

DECLARE @JohnShift1 INT = (
    SELECT TOP 1 shift_id FROM Shifts WHERE staff_id=@John AND start_time > GETDATE() ORDER BY start_time
);
DECLARE @JohnShift2 INT = (
    SELECT TOP 1 shift_id FROM Shifts WHERE staff_id=@John AND start_time > DATEADD(HOUR, 36, GETDATE()) ORDER BY start_time
);
DECLARE @RobertShift1 INT = (
    SELECT TOP 1 shift_id FROM Shifts WHERE staff_id=@Robert AND start_time > GETDATE() ORDER BY start_time
);

INSERT INTO ShiftSwapRequests (shift_id, requester_id, colleague_id, requested_at, status)
VALUES
(@JohnShift1, @John, @Alice,   DATEADD(MINUTE,-50,GETUTCDATE()), 'PENDING'),
(@JohnShift1, @John, @Mihai,   DATEADD(MINUTE,-45,GETUTCDATE()), 'PENDING'),
(@JohnShift1, @John, @Michael, DATEADD(MINUTE,-40,GETUTCDATE()), 'PENDING'),
(@JohnShift1, @John, @Sarah,   DATEADD(MINUTE,-35,GETUTCDATE()), 'PENDING'),
(@JohnShift2, @John, @David,   DATEADD(HOUR,-5,GETUTCDATE()), 'ACCEPTED'),
(@JohnShift2, @John, @Alice,   DATEADD(HOUR,-4,GETUTCDATE()), 'REJECTED'),
(@JohnShift2, @John, @Mihai,   DATEADD(HOUR,-3,GETUTCDATE()), 'CANCELLED'),
(@RobertShift1, @Robert, @Elena,  DATEADD(MINUTE,-30,GETUTCDATE()), 'PENDING'),
(@RobertShift1, @Robert, @Victor, DATEADD(HOUR,-2,GETUTCDATE()), 'ACCEPTED'),
(@RobertShift1, @Robert, @Jane,   DATEADD(HOUR,-1,GETUTCDATE()), 'REJECTED');
GO

/* =========================
   NOTIFICATIONS DATA
   ========================= */
DECLARE @John INT      = (SELECT staff_id FROM Staff WHERE first_name='John' AND last_name='Smith');
DECLARE @Alice INT     = (SELECT staff_id FROM Staff WHERE first_name='Alice' AND last_name='Jones');
DECLARE @Mihai INT     = (SELECT staff_id FROM Staff WHERE first_name='Mihai' AND last_name='Popescu');
DECLARE @Michael INT   = (SELECT staff_id FROM Staff WHERE first_name='Michael' AND last_name='Scott');
DECLARE @Sarah INT     = (SELECT staff_id FROM Staff WHERE first_name='Sarah' AND last_name='Jenkins');
DECLARE @Robert INT    = (SELECT staff_id FROM Staff WHERE first_name='Robert' AND last_name='White');
DECLARE @Elena INT     = (SELECT staff_id FROM Staff WHERE first_name='Elena' AND last_name='Radu');

DECLARE @JohnShift1 INT = (
    SELECT TOP 1 shift_id FROM Shifts WHERE staff_id=@John AND start_time > GETDATE() ORDER BY start_time
);

INSERT INTO Notifications (recipient_staff_id, title, message, created_at, is_read)
VALUES
(@Alice,  'New Shift Swap Request', 'John Smith requested a swap for shift #' + CAST(@JohnShift1 AS VARCHAR(20)) + '.', DATEADD(MINUTE,-49,GETUTCDATE()), 0),
(@Mihai,  'New Shift Swap Request', 'John Smith requested a swap for shift #' + CAST(@JohnShift1 AS VARCHAR(20)) + '.', DATEADD(MINUTE,-44,GETUTCDATE()), 0),
(@Michael,'New Shift Swap Request', 'John Smith requested a swap for shift #' + CAST(@JohnShift1 AS VARCHAR(20)) + '.', DATEADD(MINUTE,-39,GETUTCDATE()), 0),
(@Sarah,  'New Shift Swap Request', 'John Smith requested a swap for shift #' + CAST(@JohnShift1 AS VARCHAR(20)) + '.', DATEADD(MINUTE,-34,GETUTCDATE()), 0),
(@Elena,  'New Shift Swap Request', 'Robert White requested a pharmacy swap request.', DATEADD(MINUTE,-29,GETUTCDATE()), 0),
(@John,   'Shift Swap Accepted', 'Your request to David Bradley was accepted.', DATEADD(HOUR,-4,GETUTCDATE()), 1),
(@John,   'Shift Swap Rejected', 'Your request to Alice Jones was rejected.', DATEADD(HOUR,-3,GETUTCDATE()), 0),
(@John,   'Shift Swap Cancelled', 'You cancelled your request to Mihai Popescu.', DATEADD(HOUR,-2,GETUTCDATE()), 1),
(@Robert, 'Shift Swap Accepted', 'Victor Marin accepted your pharmacy swap request.', DATEADD(HOUR,-1,GETUTCDATE()), 0),
(@Robert, 'Shift Swap Rejected', 'Jane Doe rejected your pharmacy swap request.', DATEADD(MINUTE,-50,GETUTCDATE()), 0);
GO

/* =========================
   ER REQUESTS
   ========================= */
DECLARE @Alice INT = (SELECT staff_id FROM Staff WHERE first_name='Alice' AND last_name='Jones');
DECLARE @Emma INT  = (SELECT staff_id FROM Staff WHERE first_name='Emma' AND last_name='Thompson');

INSERT INTO ER_Requests (specialization, [location], created_at, [status], assigned_doctor_id, assigned_doctor_name)
VALUES
('Surgeon', 'Ward A', '2026-03-30 09:45:00', 'Pending', null, null),
('Cardiologist', 'Ward A', '2026-03-30 09:50:00', 'Pending', null, null),
('Neurology', 'Ward A', '2026-03-30 09:55:00', 'Pending', null, null),
('Surgeon', 'Ward A', '2026-03-30 09:59:00', 'Pending', null, null),
('Pediatrics', 'Ward A', '2026-03-30 10:00:00', 'Pending', null, null),
('Cardiologist', 'ICU', '2026-04-01 08:30:00', 'Assigned', @Alice, 'Alice Jones'),
('Surgeon', 'ER Room 4', '2026-04-01 09:00:00', 'Completed', @Emma, 'Emma Thompson');
GO

/* =========================
   VALIDATION
   ========================= */
SELECT * FROM Staff ORDER BY staff_id;
SELECT * FROM Shifts ORDER BY shift_id;
SELECT * FROM ShiftSwapRequests ORDER BY swap_id;
SELECT * FROM Notifications ORDER BY notification_id;
SELECT * FROM ER_Requests ORDER BY request_id;
GO