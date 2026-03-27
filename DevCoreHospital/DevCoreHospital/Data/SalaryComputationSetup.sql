USE DevCoreHospital;
GO

-- Create Doctors Table
IF OBJECT_ID(N'dbo.Doctors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Doctors (
        StaffID INT NOT NULL PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        IsAvailable BIT NOT NULL DEFAULT (1)
    );
END
GO

-- Create PharmacyStaff Table
IF OBJECT_ID(N'dbo.PharmacyStaff', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyStaff (
        StaffID INT NOT NULL PRIMARY KEY,
        DisplayName NVARCHAR(200) NOT NULL,
        IsAvailable BIT NOT NULL DEFAULT (1)
    );
END
GO

-- Create Shifts Table
IF OBJECT_ID(N'dbo.Shifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Shifts (
        ShiftId INT IDENTITY(1,1) PRIMARY KEY,
        StaffID INT NOT NULL, 
        Location NVARCHAR(100) NOT NULL,
        StartTime DATETIME2 NOT NULL,
        EndTime DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL
    );
END
GO

-- Create MedicineSales Table
IF OBJECT_ID(N'dbo.MedicineSales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MedicineSales (
        SaleId INT IDENTITY(1,1) PRIMARY KEY,
        PharmacistID INT NOT NULL,
        SaleDate DATETIME2 NOT NULL,
        Quantity INT NOT NULL
    );
END
GO

-- ==========================================
-- INSERTING DATA FOR TESTING
-- ==========================================

USE DevCoreHospital;
GO

-- Insert Doctors
IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE StaffID = 101)
BEGIN
    INSERT INTO dbo.Doctors (StaffID, FirstName, LastName, IsAvailable) VALUES
    (101, N'Gregory', N'House', 1),
    (102, N'James', N'Wilson', 1);
END
GO

-- Insert Pharmacists
IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyStaff WHERE StaffID = 201)
BEGIN
    INSERT INTO dbo.PharmacyStaff (StaffID, DisplayName, IsAvailable) VALUES
    (201, N'John Doe', 1),
    (202, N'Jane Smith', 1);
END
GO

-- Insert Shifts
IF NOT EXISTS (SELECT 1 FROM dbo.Shifts)
BEGIN
    DECLARE @Today DATETIME2 = SYSDATETIME();
    
    INSERT INTO dbo.Shifts (StaffID, Location, StartTime, EndTime, Status) VALUES
    (101, N'ER', DATEADD(DAY, -1, @Today), DATEADD(HOUR, 12, DATEADD(DAY, -1, @Today)), N'COMPLETED'),
    (101, N'Clinic', DATEADD(DAY, -3, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -3, @Today)), N'COMPLETED'),
    (101, N'ICU', DATEADD(DAY, -5, @Today), DATEADD(HOUR, 10, DATEADD(DAY, -5, @Today)), N'COMPLETED'),
    (201, N'Main Pharmacy', DATEADD(DAY, -2, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -2, @Today)), N'COMPLETED'),
    (201, N'Main Pharmacy', DATEADD(DAY, -4, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -4, @Today)), N'COMPLETED');
END
GO

-- Insert Medicine Sales
IF NOT EXISTS (SELECT 1 FROM dbo.MedicineSales)
BEGIN
    DECLARE @Today DATETIME2 = SYSDATETIME();
    
    INSERT INTO dbo.MedicineSales (PharmacistID, SaleDate, Quantity) VALUES
    (201, @Today, 15),
    (201, DATEADD(DAY, -2, @Today), 35),
    (201, DATEADD(DAY, -4, @Today), 20); 
END
GO
USE DevCoreHospital;
GO

DECLARE @Today DATETIME2 = SYSDATETIME();

-- 1. Add 8-hour shifts for Jamie (2) and Pat (3)
INSERT INTO dbo.Shifts (StaffID, Location, StartTime, EndTime, Status) VALUES
(2, N'Main Pharmacy', DATEADD(DAY, -1, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -1, @Today)), N'COMPLETED'),
(2, N'Main Pharmacy', DATEADD(DAY, -3, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -3, @Today)), N'COMPLETED'),
(3, N'ER Pharmacy', DATEADD(DAY, -2, @Today), DATEADD(HOUR, 8, DATEADD(DAY, -2, @Today)), N'COMPLETED');

-- 2. Add Medicine Sales for Jamie (2) and Pat (3)
INSERT INTO dbo.MedicineSales (PharmacistID, SaleDate, Quantity) VALUES
(2, @Today, 50),
(3, @Today, 120);
GO