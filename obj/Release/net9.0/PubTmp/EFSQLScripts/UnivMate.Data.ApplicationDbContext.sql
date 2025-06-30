IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(450) NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [ProfilePicturePath] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginDate] datetime2 NULL,
        [Role] nvarchar(max) NOT NULL,
        [StudentId] nvarchar(max) NULL,
        [Major] nvarchar(max) NULL,
        [Department] nvarchar(max) NULL,
        [Position] nvarchar(max) NULL,
        [StaffId] nvarchar(max) NULL,
        [OfficeLocation] nvarchar(max) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE TABLE [Reports] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Location] nvarchar(max) NOT NULL,
        [ImagePath] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        [ResolvedById] int NULL,
        [AssignedToId] int NULL,
        [AssignedAt] datetime2 NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reports_Users_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Reports_Users_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reports_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Department', N'Email', N'FirstName', N'LastLoginDate', N'LastName', N'Major', N'OfficeLocation', N'PasswordHash', N'Position', N'ProfilePicturePath', N'Role', N'StaffId', N'StudentId', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [CreatedAt], [Department], [Email], [FirstName], [LastLoginDate], [LastName], [Major], [OfficeLocation], [PasswordHash], [Position], [ProfilePicturePath], [Role], [StaffId], [StudentId], [Username])
    VALUES (1, ''2023-01-01T00:00:00.0000000'', NULL, N''admin@univmate.com'', N''System'', NULL, N''Administrator'', NULL, N''Main Administration'', N''6G94qKPK8LYNjnTllCqm2G3BUM08AzOK7yW30tfjrMc='', NULL, NULL, N''Admin'', N''ADM001'', NULL, N''admin'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Department', N'Email', N'FirstName', N'LastLoginDate', N'LastName', N'Major', N'OfficeLocation', N'PasswordHash', N'Position', N'ProfilePicturePath', N'Role', N'StaffId', N'StudentId', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_AssignedToId] ON [Reports] ([AssignedToId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_ResolvedById] ON [Reports] ([ResolvedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_UserId] ON [Reports] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505162745_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250505162745_InitialCreate', N'9.0.4');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505164737_AddReportStatusHistory'
)
BEGIN
    CREATE TABLE [ReportStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [ReportId] int NOT NULL,
        [OldStatus] nvarchar(max) NOT NULL,
        [NewStatus] nvarchar(max) NOT NULL,
        [ChangedBy] nvarchar(max) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ReportStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportStatusHistories_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505164737_AddReportStatusHistory'
)
BEGIN
    CREATE INDEX [IX_ReportStatusHistories_ReportId] ON [ReportStatusHistories] ([ReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250505164737_AddReportStatusHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250505164737_AddReportStatusHistory', N'9.0.4');
END;

COMMIT;
GO

