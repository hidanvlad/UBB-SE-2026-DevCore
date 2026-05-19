USE master;
GO

IF DB_ID('HospitalDatabaseTest') IS NOT NULL
BEGIN
    ALTER DATABASE HospitalDatabaseTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE HospitalDatabaseTest;
END
GO

CREATE DATABASE HospitalDatabaseTest;
GO

USE HospitalDatabaseTest;
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
   STATIC REFERENCE DATA
   ========================= */
INSERT INTO High_Risk_Medicines (medicine_name, warning_message)
VALUES
('Warfarin', 'Blood thinner conflict: High risk of internal bleeding.'),
('Insulin', 'Glucose conflict: Requires immediate sugar level monitoring.'),
('Penicillin', 'Allergy Warning: History of anaphylaxis in this department.');
GO
