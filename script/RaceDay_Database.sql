/* =========================================================================
   RaceDay Database Script
   Module: PROG6212 - Programming 2B - PoE Part 1, Section C
   Target: SQL Server (SSMS)
   This script creates the RaceDay schema (matching /docs/RaceDay_ERD.png)
   and seeds it with realistic sample data.
   Run on a clean SQL Server instance. Safe to re-run: drops existing
   objects first.
   ========================================================================= */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* ---------- Drop tables if they already exist (child -> parent order) ---------- */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.EventEnrolments', 'U') IS NOT NULL DROP TABLE dbo.EventEnrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

/* =========================================================================
   TABLE: Roles
   ========================================================================= */
CREATE TABLE dbo.Roles (
    RoleId      INT             IDENTITY(1,1)   NOT NULL,
    RoleName    VARCHAR(20)     NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

/* =========================================================================
   TABLE: Users
   ========================================================================= */
CREATE TABLE dbo.Users (
    UserId              INT             IDENTITY(1,1)   NOT NULL,
    RoleId              INT             NOT NULL,
    FullName            VARCHAR(100)    NOT NULL,
    Email               VARCHAR(150)    NOT NULL,
    PasswordHash        VARCHAR(256)    NOT NULL,
    PhoneNumber         VARCHAR(20)     NULL,
    ProfilePictureUrl   VARCHAR(300)    NULL,
    CreatedAt           DATETIME        NOT NULL        DEFAULT GETDATE(),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);
GO

/* =========================================================================
   TABLE: Events
   ========================================================================= */
CREATE TABLE dbo.Events (
    EventId         INT             IDENTITY(1,1)   NOT NULL,
    OrganiserId     INT             NOT NULL,
    Name            VARCHAR(150)    NOT NULL,
    Description     VARCHAR(1000)   NULL,
    EventDate       DATETIME        NOT NULL,
    Location        VARCHAR(150)    NOT NULL,
    DistanceKm      DECIMAL(6,2)    NOT NULL,
    EventType       VARCHAR(10)     NOT NULL,
    BannerImageUrl  VARCHAR(300)    NULL,
    CreatedAt       DATETIME        NOT NULL        DEFAULT GETDATE(),
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle'))
);
GO

/* =========================================================================
   TABLE: Categories
   ========================================================================= */
CREATE TABLE dbo.Categories (
    CategoryId  INT             IDENTITY(1,1)   NOT NULL,
    EventId     INT             NOT NULL,
    Name        VARCHAR(50)     NOT NULL,
    MinAge      INT             NULL,
    MaxAge      INT             NULL,
    DistanceKm  DECIMAL(6,2)    NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId)
);
GO

/* =========================================================================
   TABLE: EventEnrolments
   ========================================================================= */
CREATE TABLE dbo.EventEnrolments (
    EnrolmentId     INT             IDENTITY(1,1)   NOT NULL,
    EventId         INT             NOT NULL,
    ParticipantId   INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL        DEFAULT GETDATE(),
    Status          VARCHAR(20)     NOT NULL        DEFAULT 'Pending',
    CONSTRAINT PK_EventEnrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_EventParticipant UNIQUE (EventId, ParticipantId),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled'))
);
GO

/* =========================================================================
   TABLE: Results
   ========================================================================= */
CREATE TABLE dbo.Results (
    ResultId                INT             IDENTITY(1,1)   NOT NULL,
    EnrolmentId             INT             NOT NULL,
    CapturedByOrganiserId   INT             NOT NULL,
    FinishTime              TIME            NOT NULL,
    FinishPosition          INT             NOT NULL,
    CapturedDate            DATETIME        NOT NULL        DEFAULT GETDATE(),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.EventEnrolments(EnrolmentId),
    CONSTRAINT FK_Results_Organiser FOREIGN KEY (CapturedByOrganiserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId)
);
GO

/* =========================================================================
   SEED DATA
   ========================================================================= */

-- Roles
INSERT INTO dbo.Roles (RoleName) VALUES ('Organiser'), ('Participant');
GO

-- Users: 2 Organisers, 2 Participants
-- NOTE: PasswordHash values below are placeholders representing already-hashed
-- passwords (e.g. produced by BCrypt in the Part 2 API) - never store plaintext.
INSERT INTO dbo.Users (RoleId, FullName, Email, PasswordHash, PhoneNumber)
VALUES
    ((SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Organiser'), 'Naledi Khumalo', 'naledi.khumalo@raceday.co.za', '$2a$11$examplehashvalue001', '0821234567'),
    ((SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Organiser'), 'Pieter van Wyk', 'pieter.vanwyk@raceday.co.za', '$2a$11$examplehashvalue002', '0837654321'),
    ((SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Participant'), 'Thandiwe Nkosi', 'thandiwe.nkosi@gmail.com', '$2a$11$examplehashvalue003', '0721112222'),
    ((SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Participant'), 'Sipho Dlamini', 'sipho.dlamini@gmail.com', '$2a$11$examplehashvalue004', '0733334444');
GO

-- Events: 3 events
INSERT INTO dbo.Events (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType, BannerImageUrl)
VALUES
    ((SELECT UserId FROM dbo.Users WHERE Email = 'naledi.khumalo@raceday.co.za'),
        'Benoni Park Run Challenge', 'A community run through Benoni Lake and surrounding parkland.', '2026-10-10 07:00:00', 'Benoni, Gauteng', 10.0, 'Run', NULL),
    ((SELECT UserId FROM dbo.Users WHERE Email = 'naledi.khumalo@raceday.co.za'),
        'Gauteng Charity Cycle Tour', 'A scenic cycling event raising funds for local schools.', '2026-11-01 06:30:00', 'Pretoria, Gauteng', 42.0, 'Cycle', NULL),
    ((SELECT UserId FROM dbo.Users WHERE Email = 'pieter.vanwyk@raceday.co.za'),
        'Cape Winelands Fun Walk', 'A family-friendly walk through the vineyards of Stellenbosch.', '2026-09-20 08:00:00', 'Stellenbosch, Western Cape', 5.0, 'Walk', NULL);
GO

-- Categories: at least one per event
INSERT INTO dbo.Categories (EventId, Name, MinAge, MaxAge, DistanceKm)
VALUES
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge'), 'Under 20', 10, 19, 10.0),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge'), 'Senior', 20, 59, 10.0),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge'), 'Veteran (60+)', 60, 120, 10.0),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Gauteng Charity Cycle Tour'), '42km Road', NULL, NULL, 42.0),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Gauteng Charity Cycle Tour'), '21km Half', NULL, NULL, 21.0),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Cape Winelands Fun Walk'), 'Family (all ages)', NULL, NULL, 5.0);
GO

-- Event Enrolments: sample enrolments
INSERT INTO dbo.EventEnrolments (EventId, ParticipantId, CategoryId, Status)
VALUES
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge'),
     (SELECT UserId FROM dbo.Users WHERE Email = 'thandiwe.nkosi@gmail.com'),
     (SELECT CategoryId FROM dbo.Categories WHERE Name = 'Senior' AND EventId = (SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge')),
     'Confirmed'),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Gauteng Charity Cycle Tour'),
     (SELECT UserId FROM dbo.Users WHERE Email = 'sipho.dlamini@gmail.com'),
     (SELECT CategoryId FROM dbo.Categories WHERE Name = '42km Road'),
     'Confirmed'),
    ((SELECT EventId FROM dbo.Events WHERE Name = 'Cape Winelands Fun Walk'),
     (SELECT UserId FROM dbo.Users WHERE Email = 'thandiwe.nkosi@gmail.com'),
     (SELECT CategoryId FROM dbo.Categories WHERE Name = 'Family (all ages)'),
     'Pending');
GO

-- Results: sample captured result for the completed enrolment
INSERT INTO dbo.Results (EnrolmentId, CapturedByOrganiserId, FinishTime, FinishPosition)
VALUES
    ((SELECT EnrolmentId FROM dbo.EventEnrolments WHERE EventId = (SELECT EventId FROM dbo.Events WHERE Name = 'Benoni Park Run Challenge')
        AND ParticipantId = (SELECT UserId FROM dbo.Users WHERE Email = 'thandiwe.nkosi@gmail.com')),
     (SELECT UserId FROM dbo.Users WHERE Email = 'naledi.khumalo@raceday.co.za'),
     '00:52:14', 47);
GO

/* =========================================================================
   Verification queries (optional - comment out before submission if desired)
   ========================================================================= */
-- SELECT * FROM dbo.Roles;
-- SELECT * FROM dbo.Users;
-- SELECT * FROM dbo.Events;
-- SELECT * FROM dbo.Categories;
-- SELECT * FROM dbo.EventEnrolments;
-- SELECT * FROM dbo.Results;
