/*
  Pharmacy handover: Pending_Medications + PharmacyStaff + PharmacyShifts
  Run against DevCoreHospital (see AppSettings connection string).
*/
IF OBJECT_ID(N'dbo.PharmacyStaff', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyStaff (
        StaffID       INT           NOT NULL PRIMARY KEY,
        DisplayName   NVARCHAR(200) NOT NULL,
        IsAvailable   BIT           NOT NULL CONSTRAINT DF_PharmacyStaff_IsAvailable DEFAULT (1)
    );
END
GO

IF OBJECT_ID(N'dbo.Pending_Medications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pending_Medications (
        Id                   INT IDENTITY(1,1) PRIMARY KEY,
        ResponsibleStaffID   INT NOT NULL,
        OrderStatus          NVARCHAR(50) NOT NULL,
        CONSTRAINT FK_Pending_Medications_Staff FOREIGN KEY (ResponsibleStaffID)
            REFERENCES dbo.PharmacyStaff (StaffID)
    );
    CREATE INDEX IX_Pending_Medications_Staff_Status
        ON dbo.Pending_Medications (ResponsibleStaffID, OrderStatus);
END
GO

IF OBJECT_ID(N'dbo.PharmacyShifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyShifts (
        ShiftId       INT IDENTITY(1,1) PRIMARY KEY,
        StaffID       INT NOT NULL,
        StartDateTime DATETIME2 NOT NULL,
        EndDateTime   DATETIME2 NOT NULL,
        Status        NVARCHAR(20) NOT NULL,
        CONSTRAINT FK_PharmacyShifts_Staff FOREIGN KEY (StaffID)
            REFERENCES dbo.PharmacyStaff (StaffID)
    );
    CREATE INDEX IX_PharmacyShifts_Staff_Status ON dbo.PharmacyShifts (StaffID, Status);
END
GO

/* Seed (idempotent-ish): only insert staff if empty */
IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyStaff)
BEGIN
    INSERT INTO dbo.PharmacyStaff (StaffID, DisplayName, IsAvailable) VALUES
        (1, N'Current User (demo)', 1),
        (2, N'Jamie Chen', 1),
        (3, N'Pat Moore', 1),
        (4, N'Alex Rivera', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Pending_Medications)
BEGIN
    INSERT INTO dbo.Pending_Medications (ResponsibleStaffID, OrderStatus) VALUES
        (1, N'Processing'),
        (1, N'Processing'),
        (1, N'Completed');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PharmacyShifts WHERE StaffID = 1 AND Status = N'Active')
BEGIN
    DECLARE @start DATETIME2 = CAST(CAST(SYSDATETIME() AS DATE) AS DATETIME2);
    DECLARE @end   DATETIME2 = DATEADD(HOUR, 8, @start);
    INSERT INTO dbo.PharmacyShifts (StaffID, StartDateTime, EndDateTime, Status)
    VALUES (1, @start, @end, N'Active');
END
GO
