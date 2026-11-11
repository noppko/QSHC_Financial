-- ตารางของระบบ Financial — รันบนฐาน QSHC_Financial (10.67.67.162) เท่านั้น ไม่ใช่ HIS
-- สร้างจาก: dotnet ef migrations script --idempotent (รันซ้ำได้) | แอปจะ migrate เองเมื่อ Database:AutoMigrate = true
USE QSHC_Financial;
GO

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
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'00000000000000_CreateIdentitySchema', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715063510_InitialWithReferenceValues'
)
BEGIN
    CREATE TABLE [ReferenceValues] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(100) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [NameTH] nvarchar(200) NOT NULL,
        [NameEN] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        [DisplayOrder] int NOT NULL,
        [IsSystem] bit NOT NULL,
        [ParentCategory] nvarchar(100) NULL,
        [ParentId] int NULL,
        [Metadata] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ReferenceValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReferenceValues_ReferenceValues_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ReferenceValues] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715063510_InitialWithReferenceValues'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (1, N''TITLE'', N''MR'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 1, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Mr.'', N''นาย'', NULL, NULL, NULL, NULL),
    (2, N''TITLE'', N''MRS'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 2, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Mrs.'', N''นาง'', NULL, NULL, NULL, NULL),
    (3, N''TITLE'', N''MISS'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 3, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Miss'', N''นางสาว'', NULL, NULL, NULL, NULL),
    (4, N''TITLE'', N''DR'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 4, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Dr.'', N''นายแพทย์/แพทย์หญิง'', NULL, NULL, NULL, NULL),
    (5, N''GENDER'', N''MALE'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 1, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Male'', N''ชาย'', NULL, NULL, NULL, NULL),
    (6, N''GENDER'', N''FEMALE'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 2, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Female'', N''หญิง'', NULL, NULL, NULL, NULL),
    (7, N''GENDER'', N''OTHER'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 3, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Other'', N''อื่นๆ'', NULL, NULL, NULL, NULL),
    (8, N''BLOOD_TYPE'', N''A'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 1, CAST(1 AS bit), CAST(1 AS bit), NULL, N''A'', N''A'', NULL, NULL, NULL, NULL),
    (9, N''BLOOD_TYPE'', N''B'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 2, CAST(1 AS bit), CAST(1 AS bit), NULL, N''B'', N''B'', NULL, NULL, NULL, NULL),
    (10, N''BLOOD_TYPE'', N''AB'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 3, CAST(1 AS bit), CAST(1 AS bit), NULL, N''AB'', N''AB'', NULL, NULL, NULL, NULL),
    (11, N''BLOOD_TYPE'', N''O'', ''2025-01-01T00:00:00.0000000Z'', N''System'', NULL, 4, CAST(1 AS bit), CAST(1 AS bit), NULL, N''O'', N''O'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715063510_InitialWithReferenceValues'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ReferenceValues_Category_Code] ON [ReferenceValues] ([Category], [Code]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715063510_InitialWithReferenceValues'
)
BEGIN
    CREATE INDEX [IX_ReferenceValues_ParentId] ON [ReferenceValues] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715063510_InitialWithReferenceValues'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715063510_InitialWithReferenceValues', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ReferenceValues]') AND [c].[name] = N'IsActive');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ReferenceValues] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [ReferenceValues] ADD DEFAULT CAST(1 AS bit) FOR [IsActive];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    CREATE TABLE [AppSettings] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(4000) NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    CREATE TABLE [NhsoFiscalSummaries] (
        [Id] int NOT NULL IDENTITY,
        [FiscalYear] int NOT NULL,
        [PatientTypeCode] nvarchar(10) NOT NULL,
        [PayorCategoryCode] nvarchar(20) NOT NULL,
        [AgreementName] nvarchar(400) NOT NULL,
        [Visits] int NOT NULL,
        [Bills] int NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [RefreshedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_NhsoFiscalSummaries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (12, N''PATIENT_TYPE'', N''O'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 1, CAST(1 AS bit), CAST(1 AS bit), NULL, N''OPD'', N''ผู้ป่วยนอก'', NULL, NULL, NULL, NULL),
    (13, N''PATIENT_TYPE'', N''I'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 2, CAST(1 AS bit), CAST(1 AS bit), NULL, N''IPD'', N''ผู้ป่วยใน'', NULL, NULL, NULL, NULL),
    (14, N''ACCESS_LEVEL'', N''ADMIN'', ''2025-01-01T00:00:00.0000000'', N''System'', N''ตั้งค่าระบบได้ และดูได้ทุกหน้า'', 1, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Administrator'', N''ผู้ดูแลระบบ'', NULL, NULL, NULL, NULL),
    (15, N''ACCESS_LEVEL'', N''FINANCE'', ''2025-01-01T00:00:00.0000000'', N''System'', N''role ขึ้นต้นด้วย Cashier — เข้าเมนูการเงินได้'', 2, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Finance'', N''เจ้าหน้าที่การเงิน'', NULL, NULL, NULL, NULL),
    (16, N''ACCESS_LEVEL'', N''GENERAL'', ''2025-01-01T00:00:00.0000000'', N''System'', N''ดูได้เฉพาะ Dashboard'', 3, CAST(1 AS bit), CAST(1 AS bit), NULL, N''General'', N''ผู้ใช้งานทั่วไป (Dashboard)'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AppSettings_Key] ON [AppSettings] ([Key]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    CREATE INDEX [IX_NhsoFiscalSummaries_FiscalYear_PatientTypeCode] ON [NhsoFiscalSummaries] ([FiscalYear], [PatientTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022084628_AddAppSettingsAndNhsoFiscalSummaries', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022095209_AddAgreementPatientTypeCode'
)
BEGIN
    ALTER TABLE [NhsoFiscalSummaries] ADD [AgreementPatientTypeCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022095209_AddAgreementPatientTypeCode'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022095209_AddAgreementPatientTypeCode', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024184703_RenameAgreementEncounterCode'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NhsoFiscalSummaries]') AND [c].[name] = N'AgreementPatientTypeCode');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [NhsoFiscalSummaries] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [NhsoFiscalSummaries] DROP COLUMN [AgreementPatientTypeCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024184703_RenameAgreementEncounterCode'
)
BEGIN
    ALTER TABLE [NhsoFiscalSummaries] ADD [AgreementEncounterCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024184703_RenameAgreementEncounterCode'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024184703_RenameAgreementEncounterCode', N'10.0.13');
END;

COMMIT;
GO

