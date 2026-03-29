DROP TABLE IF EXISTS Evaluation_Symptoms;
DROP TABLE IF EXISTS Evaluation_Medications;
DROP TABLE IF EXISTS Hangout_Participants;
DROP TABLE IF EXISTS [Shifts];
DROP TABLE IF EXISTS Appointments;
DROP TABLE IF EXISTS Medical_Evaluations;
DROP TABLE IF EXISTS Hangouts;
DROP TABLE IF EXISTS Staff;


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

INSERT INTO Staff ([role], department, first_name, last_name, is_available, specialization, status, contact_info, license_number, years_of_experience)
VALUES 
('Doctor', 'Cardiology', 'John', 'Smith', 1, 'Cardiologist', 'Available', 'info1', 'A1234', 20),
('Doctor', 'Emergency', 'Alice', 'Jones', 1, 'Surgeon', 'Available', 'info2', 'B4321', 15);

    
INSERT INTO Staff ([role], department, first_name, last_name, is_available, status, contact_info, certification, years_of_experience)
VALUES 
('Pharmacist', 'Pharmacy', 'Robert', 'White', 0, 'Off_Duty', 'info3', 'BPS', 13);


INSERT INTO Shifts (staff_id, location, start_time, end_time, status, is_active)
VALUES (2, 'Ward A', '2026-04-01 08:00:00', '2026-04-01 16:00:00', 'Scheduled', 1);



INSERT INTO Appointments (patient_id, doctor_id, start_time, end_time, status)
VALUES (500, 1, '2026-04-05 10:30:00', '2026-04-05 11:30:00', 'Confirmed');


INSERT INTO Medical_Evaluations (doctor_id, patient_id, diagnosis, doctor_notes, source, assumed_risk)
VALUES (1, 500, 'Mild Hypertension', 'Patient advised to reduce salt intake.', 'Physical Exam', 0);

INSERT INTO Hangouts (title, description, date_time, max_staff)
VALUES ('Friday Pizza', 'Weekly team bonding in the breakroom', '2026-04-03 17:00:00', 10);


INSERT INTO Hangout_Participants (hangout_id, staff_id)
VALUES (1, 1), (1, 2);

select * from staff