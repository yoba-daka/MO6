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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [Comments] (
        [ID] int NOT NULL IDENTITY,
        [time] datetime2 NOT NULL,
        [parentID] int NOT NULL,
        [parentCommentID] int NULL,
        [userID] int NULL,
        [name] nvarchar(max) NOT NULL,
        [title] nvarchar(max) NOT NULL,
        [content] nvarchar(max) NOT NULL,
        [managment] bit NOT NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY ([ID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [Contacts] (
        [ID] int NOT NULL IDENTITY,
        [submitTime] datetime2 NOT NULL,
        [name] nvarchar(max) NOT NULL,
        [familyName] nvarchar(max) NOT NULL,
        [email] nvarchar(max) NOT NULL,
        [phone] nvarchar(max) NOT NULL,
        [content] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Contacts] PRIMARY KEY ([ID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [EmailVerifications] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [TimesSent] int NOT NULL,
        [Created] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailVerifications] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [Memberships] (
        [Id] int NOT NULL IDENTITY,
        [memberID] nvarchar(max) NOT NULL,
        [phone] nvarchar(max) NOT NULL,
        [expiration] datetime2 NOT NULL,
        [isMonthly] bit NOT NULL,
        [isMonthlyActive] bit NOT NULL,
        [transactions] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Memberships] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [NameChanges] (
        [ID] int NOT NULL IDENTITY,
        [memberId] nvarchar(max) NOT NULL,
        [TimesSent] int NOT NULL,
        [Created] datetime2 NOT NULL,
        CONSTRAINT [PK_NameChanges] PRIMARY KEY ([ID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [PasswordResets] (
        [ID] int NOT NULL IDENTITY,
        [Email] nvarchar(max) NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [TimesSent] int NOT NULL,
        [Created] datetime2 NOT NULL,
        CONSTRAINT [PK_PasswordResets] PRIMARY KEY ([ID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [SasTokens] (
        [Id] int NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [TokenExpiration] datetime2 NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [ContainerName] nvarchar(max) NOT NULL,
        [BlobName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_SasTokens] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [TimeKeepers] (
        [Id] int NOT NULL IDENTITY,
        [date] datetime2 NOT NULL,
        [memberID] nvarchar(max) NOT NULL,
        [lessonId] int NOT NULL,
        [time] nvarchar(max) NOT NULL,
        [isVideo] bit NOT NULL,
        CONSTRAINT [PK_TimeKeepers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    CREATE TABLE [Transactions] (
        [ID] int NOT NULL IDENTITY,
        [Created] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [StatusCode] int NULL,
        [TransactionId] int NULL,
        [TransactionToken] nvarchar(max) NOT NULL,
        [TransactionTypeId] int NULL,
        [PaymentType] int NULL,
        [Sum] real NULL,
        [FirstPaymentSum] real NULL,
        [PeriodicalPaymentSum] real NULL,
        [PaymentsNum] int NULL,
        [AllPaymentsNum] int NULL,
        [PaymentDate] nvarchar(max) NOT NULL,
        [Asmachta] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [PayerPhone] nvarchar(max) NOT NULL,
        [PayerEmail] nvarchar(max) NOT NULL,
        [CardSuffix] nvarchar(max) NOT NULL,
        [CardType] nvarchar(max) NOT NULL,
        [CardTypeCode] int NULL,
        [CardBrand] nvarchar(max) NOT NULL,
        [CardBrandCode] int NULL,
        [CardExp] nvarchar(max) NOT NULL,
        [ProcessId] int NULL,
        [ProcessToken] nvarchar(max) NOT NULL,
        [CardToken] nvarchar(max) NOT NULL,
        [DirectDebitId] int NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([ID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250320173349_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250320173349_InitialCreate', N'8.0.14');
END;
GO

COMMIT;
GO

