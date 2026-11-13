-- ตารางของระบบ Financial — รันบนฐาน QSHC_Financial (10.67.67.162) เท่านั้น ไม่ใช่ HIS
-- สร้างจาก: dotnet ef migrations script --idempotent (รันซ้ำได้) | แอปจะ migrate เองเมื่อ Database:AutoMigrate = true
-- RemoveAspNetIdentity: ลบตาราง AspNet* (ไม่ได้ใช้ — ยืนยันตัวตนผ่าน External Auth API)
-- FiscalReportsBySchemeAndPatients: แทน NhsoFiscalSummaries ด้วย FiscalSchemeTotals/FiscalPayorSummaries (ต้องกด "ดึงข้อมูลจาก HIS" ใหม่)
-- AddLgoPayorScheme: เพิ่มสิทธิ์ อปท. (LGO) ใน ReferenceValues (PAYOR_SCHEME)
-- AddReceivables: ตารางลูกหนี้สิทธิ ArReceivables / ArStatusHistories + สถานะ AR_STATUS ใน ReferenceValues (ส่วนงาน Areas/Receivables)
-- AddRemittanceImports: นำเข้าผลการเรียกเก็บ ArImportBatches / ArImportRows / ArPayments + AR_MATCH_RESULT, AR_IMPORT_STATUS, AR_PAYMENT_SOURCE
-- AddClaimBatchesAndReviewFlag: ชุดเรียกเก็บ ArClaimBatches / ArClaimBatchItems, ArImportBatches.ClaimBatchId, ธงต้องตรวจ ArReceivables.ReviewReason/ReviewFlaggedAt + AR_CLAIM_BATCH_STATUS
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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetRoleClaims];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetUserClaims];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetUserLogins];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetUserRoles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetUserTokens];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetRoles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    DROP TABLE [AspNetUsers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111033718_RemoveAspNetIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111033718_RemoveAspNetIdentity', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    DROP TABLE [NhsoFiscalSummaries];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    CREATE TABLE [FiscalPayorSummaries] (
        [Id] int NOT NULL IDENTITY,
        [FiscalYear] int NOT NULL,
        [SchemeCode] nvarchar(20) NOT NULL,
        [PatientTypeCode] nvarchar(10) NOT NULL,
        [VisitEncounterCode] nvarchar(20) NULL,
        [MovedFromBillSide] bit NOT NULL,
        [PayorCategoryCode] nvarchar(20) NOT NULL,
        [AgreementName] nvarchar(400) NOT NULL,
        [Patients] int NOT NULL,
        [Visits] int NOT NULL,
        [Bills] int NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [RefreshedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_FiscalPayorSummaries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    CREATE TABLE [FiscalSchemeTotals] (
        [Id] int NOT NULL IDENTITY,
        [FiscalYear] int NOT NULL,
        [SchemeCode] nvarchar(20) NOT NULL,
        [PatientTypeCode] nvarchar(10) NOT NULL,
        [ZoneCode] nvarchar(10) NOT NULL,
        [Patients] int NOT NULL,
        [Visits] int NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [RefreshedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_FiscalSchemeTotals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (17, N''PAYOR_SCHEME'', N''NHSO'', ''2025-01-01T00:00:00.0000000'', N''System'', N''หลักประกันสุขภาพถ้วนหน้า'', 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''NHSO (UCS)'', N''สปสช.'', NULL, NULL, NULL, NULL),
    (18, N''PAYOR_SCHEME'', N''CSMBS'', ''2025-01-01T00:00:00.0000000'', N''System'', N''สวัสดิการรักษาพยาบาลข้าราชการ'', 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''CSMBS'', N''กรมบัญชีกลาง'', NULL, NULL, NULL, NULL),
    (19, N''PAYOR_SCHEME'', N''SSO'', ''2025-01-01T00:00:00.0000000'', N''System'', N''สำนักงานประกันสังคม'', 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''SSO'', N''ประกันสังคม'', NULL, NULL, NULL, NULL),
    (20, N''FISCAL_ZONE'', N''IN'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''In zone'', N''ในเขต'', NULL, NULL, NULL, NULL),
    (21, N''FISCAL_ZONE'', N''OUT'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Out of zone'', N''นอกเขต'', NULL, NULL, NULL, NULL),
    (22, N''FISCAL_ZONE'', N''ALL'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Total'', N''รวม'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    CREATE INDEX [IX_FiscalPayorSummaries_FiscalYear_SchemeCode_PatientTypeCode] ON [FiscalPayorSummaries] ([FiscalYear], [SchemeCode], [PatientTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    CREATE INDEX [IX_FiscalSchemeTotals_FiscalYear_SchemeCode_PatientTypeCode_ZoneCode] ON [FiscalSchemeTotals] ([FiscalYear], [SchemeCode], [PatientTypeCode], [ZoneCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111041041_FiscalReportsBySchemeAndPatients'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111041041_FiscalReportsBySchemeAndPatients', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045159_AddLgoPayorScheme'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (23, N''PAYOR_SCHEME'', N''LGO'', ''2025-01-01T00:00:00.0000000'', N''System'', N''องค์กรปกครองส่วนท้องถิ่น'', 40, CAST(1 AS bit), CAST(1 AS bit), NULL, N''LGO'', N''อปท.'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045159_AddLgoPayorScheme'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112045159_AddLgoPayorScheme', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE TABLE [ArReceivables] (
        [Id] int NOT NULL IDENTITY,
        [HisBillUid] bigint NOT NULL,
        [BillNumber] nvarchar(30) NOT NULL,
        [HisVisitUid] bigint NOT NULL,
        [VisitNumber] nvarchar(20) NULL,
        [Hn] nvarchar(20) NULL,
        [PatientTypeCode] nvarchar(1) NOT NULL,
        [VisitStartDate] datetime2(0) NULL,
        [DischargeDate] datetime2(0) NULL,
        [BillDate] datetime2(0) NOT NULL,
        [SchemeCode] nvarchar(10) NOT NULL,
        [PayorCode] nvarchar(50) NULL,
        [PayorName] nvarchar(200) NULL,
        [PayorCategoryCode] nvarchar(10) NULL,
        [AgreementName] nvarchar(400) NULL,
        [IsOutOfZone] bit NOT NULL,
        [BilledAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [DeniedAmount] decimal(18,2) NOT NULL,
        [WriteOffAmount] decimal(18,2) NOT NULL,
        [OutstandingAmount] decimal(18,2) NOT NULL,
        [StatusCode] nvarchar(20) NOT NULL,
        [LastSyncedAt] datetime2(0) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArReceivables] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE TABLE [ArStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [ReceivableId] int NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [ChangedAt] datetime2(0) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArStatusHistories_ArReceivables_ReceivableId] FOREIGN KEY ([ReceivableId]) REFERENCES [ArReceivables] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (24, N''AR_STATUS'', N''OPEN'', ''2025-01-01T00:00:00.0000000'', N''System'', N''ดึงจาก HIS ยังไม่ได้ส่งเรียกเก็บ'', 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Open'', N''ตั้งหนี้'', NULL, NULL, NULL, NULL),
    (25, N''AR_STATUS'', N''CLAIMED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Claimed'', N''ส่งเรียกเก็บแล้ว'', NULL, NULL, NULL, NULL),
    (26, N''AR_STATUS'', N''PARTIAL'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Partially paid'', N''ได้รับบางส่วน'', NULL, NULL, NULL, NULL),
    (27, N''AR_STATUS'', N''PAID'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 40, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Paid'', N''ได้รับครบ'', NULL, NULL, NULL, NULL),
    (28, N''AR_STATUS'', N''DENIED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 50, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Denied'', N''ถูกปฏิเสธ'', NULL, NULL, NULL, NULL),
    (29, N''AR_STATUS'', N''APPEAL'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 60, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Appeal'', N''อุทธรณ์/ส่งใหม่'', NULL, NULL, NULL, NULL),
    (30, N''AR_STATUS'', N''WRITE_OFF'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 70, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Write-off'', N''ตัดหนี้'', NULL, NULL, NULL, NULL),
    (31, N''AR_STATUS'', N''CANCELLED'', ''2025-01-01T00:00:00.0000000'', N''System'', N''บิลถูกยกเลิก/คืนเงินใน HIS หลังตั้งหนี้'', 80, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Cancelled'', N''ยกเลิก'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE INDEX [IX_ArReceivables_BillDate] ON [ArReceivables] ([BillDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ArReceivables_HisBillUid] ON [ArReceivables] ([HisBillUid]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE INDEX [IX_ArReceivables_SchemeCode_Hn_VisitStartDate] ON [ArReceivables] ([SchemeCode], [Hn], [VisitStartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE INDEX [IX_ArReceivables_SchemeCode_StatusCode_BillDate] ON [ArReceivables] ([SchemeCode], [StatusCode], [BillDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE INDEX [IX_ArReceivables_SchemeCode_VisitNumber] ON [ArReceivables] ([SchemeCode], [VisitNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    CREATE INDEX [IX_ArStatusHistories_ReceivableId] ON [ArStatusHistories] ([ReceivableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113032054_AddReceivables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113032054_AddReceivables', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE TABLE [ArImportBatches] (
        [Id] int NOT NULL IDENTITY,
        [SchemeCode] nvarchar(10) NOT NULL,
        [StatementNo] nvarchar(100) NULL,
        [TransferDate] date NULL,
        [FileName] nvarchar(260) NOT NULL,
        [FileSha256] nvarchar(64) NOT NULL,
        [RowCount] int NOT NULL,
        [TotalReceived] decimal(18,2) NOT NULL,
        [StatusCode] nvarchar(20) NOT NULL,
        [ConfirmedBy] nvarchar(100) NULL,
        [ConfirmedAt] datetime2(0) NULL,
        [CancelledBy] nvarchar(100) NULL,
        [CancelledAt] datetime2(0) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArImportBatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE TABLE [ArPayments] (
        [Id] int NOT NULL IDENTITY,
        [ReceivableId] int NOT NULL,
        [ImportBatchId] int NULL,
        [ImportRowId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [DeniedAmount] decimal(18,2) NOT NULL,
        [ReasonCode] nvarchar(50) NULL,
        [ReasonText] nvarchar(500) NULL,
        [TransferDate] date NULL,
        [StatementNo] nvarchar(100) NULL,
        [SourceCode] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArPayments_ArReceivables_ReceivableId] FOREIGN KEY ([ReceivableId]) REFERENCES [ArReceivables] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE TABLE [ArImportRows] (
        [Id] int NOT NULL IDENTITY,
        [ImportBatchId] int NOT NULL,
        [RowNo] int NOT NULL,
        [VisitNumber] nvarchar(20) NULL,
        [Hn] nvarchar(20) NULL,
        [VisitDate] date NULL,
        [DischargeDate] date NULL,
        [BillNumber] nvarchar(30) NULL,
        [ClaimedAmount] decimal(18,2) NULL,
        [ReceivedAmount] decimal(18,2) NOT NULL,
        [DeniedAmount] decimal(18,2) NOT NULL,
        [ReasonCode] nvarchar(50) NULL,
        [ReasonText] nvarchar(500) NULL,
        [StatementNo] nvarchar(100) NULL,
        [TransferDate] date NULL,
        [Note] nvarchar(500) NULL,
        [MatchResultCode] nvarchar(20) NOT NULL,
        [MatchStep] nvarchar(10) NULL,
        [ReceivableId] int NULL,
        [CandidateIds] nvarchar(400) NULL,
        [Message] nvarchar(500) NULL,
        [IsAccepted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArImportRows] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArImportRows_ArImportBatches_ImportBatchId] FOREIGN KEY ([ImportBatchId]) REFERENCES [ArImportBatches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ArImportRows_ArReceivables_ReceivableId] FOREIGN KEY ([ReceivableId]) REFERENCES [ArReceivables] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (32, N''AR_MATCH_RESULT'', N''MATCHED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''MATCHED'', N''จับคู่ได้'', NULL, NULL, NULL, NULL),
    (33, N''AR_MATCH_RESULT'', N''AMOUNT_DIFF'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''AMOUNT_DIFF'', N''ยอดต่าง'', NULL, NULL, NULL, NULL),
    (34, N''AR_MATCH_RESULT'', N''DENIED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''DENIED'', N''ปฏิเสธ'', NULL, NULL, NULL, NULL),
    (35, N''AR_MATCH_RESULT'', N''SUGGESTED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 40, CAST(1 AS bit), CAST(1 AS bit), NULL, N''SUGGESTED'', N''แนะนำ (ต้องยืนยัน)'', NULL, NULL, NULL, NULL),
    (36, N''AR_MATCH_RESULT'', N''MULTIPLE'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 50, CAST(1 AS bit), CAST(1 AS bit), NULL, N''MULTIPLE'', N''พบหลายรายการ'', NULL, NULL, NULL, NULL),
    (37, N''AR_MATCH_RESULT'', N''NOT_FOUND'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 60, CAST(1 AS bit), CAST(1 AS bit), NULL, N''NOT_FOUND'', N''ไม่พบ'', NULL, NULL, NULL, NULL),
    (38, N''AR_MATCH_RESULT'', N''DUPLICATE'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 70, CAST(1 AS bit), CAST(1 AS bit), NULL, N''DUPLICATE'', N''ซ้ำ'', NULL, NULL, NULL, NULL),
    (39, N''AR_MATCH_RESULT'', N''INVALID'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 80, CAST(1 AS bit), CAST(1 AS bit), NULL, N''INVALID'', N''ข้อมูลไม่ถูกต้อง'', NULL, NULL, NULL, NULL),
    (40, N''AR_IMPORT_STATUS'', N''VALIDATED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''VALIDATED'', N''รอยืนยัน'', NULL, NULL, NULL, NULL),
    (41, N''AR_IMPORT_STATUS'', N''CONFIRMED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''CONFIRMED'', N''ยืนยันแล้ว'', NULL, NULL, NULL, NULL),
    (42, N''AR_IMPORT_STATUS'', N''CANCELLED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''CANCELLED'', N''ยกเลิก'', NULL, NULL, NULL, NULL),
    (43, N''AR_PAYMENT_SOURCE'', N''IMPORT'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Import'', N''นำเข้าไฟล์ผลการเรียกเก็บ'', NULL, NULL, NULL, NULL),
    (44, N''AR_PAYMENT_SOURCE'', N''MANUAL'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Manual'', N''บันทึกเอง'', NULL, NULL, NULL, NULL),
    (45, N''AR_PAYMENT_SOURCE'', N''WRITE_OFF'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''Write-off'', N''ตัดหนี้'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArImportBatches_FileSha256] ON [ArImportBatches] ([FileSha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArImportBatches_SchemeCode_StatusCode] ON [ArImportBatches] ([SchemeCode], [StatusCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArImportRows_ImportBatchId_MatchResultCode] ON [ArImportRows] ([ImportBatchId], [MatchResultCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArImportRows_ReceivableId] ON [ArImportRows] ([ReceivableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArPayments_ImportBatchId] ON [ArPayments] ([ImportBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArPayments_ReceivableId] ON [ArPayments] ([ReceivableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    CREATE INDEX [IX_ArPayments_StatementNo_ReceivableId] ON [ArPayments] ([StatementNo], [ReceivableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113033824_AddRemittanceImports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113033824_AddRemittanceImports', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    ALTER TABLE [ArReceivables] ADD [ReviewFlaggedAt] datetime2(0) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    ALTER TABLE [ArReceivables] ADD [ReviewReason] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    ALTER TABLE [ArImportBatches] ADD [ClaimBatchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    CREATE TABLE [ArClaimBatches] (
        [Id] int NOT NULL IDENTITY,
        [BatchNo] nvarchar(40) NOT NULL,
        [SchemeCode] nvarchar(10) NOT NULL,
        [PatientTypeCode] nvarchar(1) NULL,
        [PayorFilter] nvarchar(100) NULL,
        [PeriodFrom] date NULL,
        [PeriodTo] date NULL,
        [StatusCode] nvarchar(20) NOT NULL,
        [SentDate] date NULL,
        [ReferenceDocNo] nvarchar(100) NULL,
        [SentBy] nvarchar(100) NULL,
        [ItemCount] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArClaimBatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    CREATE TABLE [ArClaimBatchItems] (
        [Id] int NOT NULL IDENTITY,
        [ClaimBatchId] int NOT NULL,
        [ReceivableId] int NOT NULL,
        [ClaimedAmount] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_ArClaimBatchItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArClaimBatchItems_ArClaimBatches_ClaimBatchId] FOREIGN KEY ([ClaimBatchId]) REFERENCES [ArClaimBatches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ArClaimBatchItems_ArReceivables_ReceivableId] FOREIGN KEY ([ReceivableId]) REFERENCES [ArReceivables] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] ON;
    EXEC(N'INSERT INTO [ReferenceValues] ([Id], [Category], [Code], [CreatedAt], [CreatedBy], [Description], [DisplayOrder], [IsActive], [IsSystem], [Metadata], [NameEN], [NameTH], [ParentCategory], [ParentId], [UpdatedAt], [UpdatedBy])
    VALUES (46, N''AR_CLAIM_BATCH_STATUS'', N''DRAFT'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 10, CAST(1 AS bit), CAST(1 AS bit), NULL, N''DRAFT'', N''จัดชุด (ยังไม่ส่ง)'', NULL, NULL, NULL, NULL),
    (47, N''AR_CLAIM_BATCH_STATUS'', N''SENT'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 20, CAST(1 AS bit), CAST(1 AS bit), NULL, N''SENT'', N''ส่งแล้ว'', NULL, NULL, NULL, NULL),
    (48, N''AR_CLAIM_BATCH_STATUS'', N''CANCELLED'', ''2025-01-01T00:00:00.0000000'', N''System'', NULL, 30, CAST(1 AS bit), CAST(1 AS bit), NULL, N''CANCELLED'', N''ยกเลิก'', NULL, NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Category', N'Code', N'CreatedAt', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'IsSystem', N'Metadata', N'NameEN', N'NameTH', N'ParentCategory', N'ParentId', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ReferenceValues]'))
        SET IDENTITY_INSERT [ReferenceValues] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ArClaimBatches_BatchNo] ON [ArClaimBatches] ([BatchNo]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    CREATE INDEX [IX_ArClaimBatches_SchemeCode_StatusCode] ON [ArClaimBatches] ([SchemeCode], [StatusCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ArClaimBatchItems_ClaimBatchId_ReceivableId] ON [ArClaimBatchItems] ([ClaimBatchId], [ReceivableId]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    CREATE INDEX [IX_ArClaimBatchItems_ReceivableId] ON [ArClaimBatchItems] ([ReceivableId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113040540_AddClaimBatchesAndReviewFlag'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113040540_AddClaimBatchesAndReviewFlag', N'10.0.13');
END;

COMMIT;
GO

