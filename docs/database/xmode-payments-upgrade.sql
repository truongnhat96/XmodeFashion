BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    ALTER TABLE [Orders] ADD [PaidAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    ALTER TABLE [Orders] ADD [PaymentExpiresAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    ALTER TABLE [Orders] ADD [PaymentMethod] nvarchar(10) NOT NULL DEFAULT N'COD';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    ALTER TABLE [Orders] ADD [PaymentStatus] nvarchar(20) NOT NULL DEFAULT N'Unpaid';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(10) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Url] nvarchar(2000) NULL,
        [TransactionId] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    CREATE INDEX [IX_Orders_PaymentStatus_PaymentExpiresAt] ON [Orders] ([PaymentStatus], [PaymentExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Provider_TransactionId] ON [Payments] ([Provider], [TransactionId]) WHERE [TransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111164616_AddFashionPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111164616_AddFashionPayments', N'9.0.9');
END;

COMMIT;
GO

