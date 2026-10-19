-- Создание базы данных
IF DB_ID('InternetShopDB_Primary') IS NULL
BEGIN
    CREATE DATABASE InternetShopDB_Primary;
END
GO

USE InternetShopDB_Primary;
GO

-- Клиенты
IF OBJECT_ID('dbo.Customers', 'U') IS NULL
CREATE TABLE dbo.Customers (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    FirstName     NVARCHAR(100)  NOT NULL,
    LastName      NVARCHAR(100)  NOT NULL,
    Email         NVARCHAR(200)  NOT NULL UNIQUE,
    Phone         NVARCHAR(50)   NULL,
    RegisteredAt  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Товары (Stock — общий ресурс)
IF OBJECT_ID('dbo.Products', 'U') IS NULL
CREATE TABLE dbo.Products (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(200)  NOT NULL,
    Description  NVARCHAR(500)  NULL,
    Price        DECIMAL(18,2)  NOT NULL CHECK (Price >= 0),
    Stock        INT            NOT NULL CHECK (Stock >= 0),
    Category     NVARCHAR(100)  NULL
);
GO

-- Заказы
IF OBJECT_ID('dbo.Orders', 'U') IS NULL
CREATE TABLE dbo.Orders (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId       INT            NOT NULL,
    CreatedAt        DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    Status           INT            NOT NULL DEFAULT 0,   -- 0=Pending,1=Paid,2=Shipped,3=Delivered,4=Cancelled
    TotalPrice       DECIMAL(18,2)  NOT NULL DEFAULT 0,
    ShippingAddress  NVARCHAR(500)  NOT NULL,
    PaidAt           DATETIME2      NULL,
    ShippedAt        DATETIME2      NULL,
    DeliveredAt      DATETIME2      NULL,
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id)
);
GO

-- Позиции заказов
IF OBJECT_ID('dbo.OrderItems', 'U') IS NULL
CREATE TABLE dbo.OrderItems (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    OrderId    INT            NOT NULL,
    ProductId  INT            NOT NULL,
    Quantity   INT            NOT NULL CHECK (Quantity > 0),
    Price      DECIMAL(18,2)  NOT NULL CHECK (Price >= 0),
    CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY (OrderId)   REFERENCES dbo.Orders(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
);
GO

-- Платежи
IF OBJECT_ID('dbo.Payments', 'U') IS NULL
CREATE TABLE dbo.Payments (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    OrderId        INT            NOT NULL,
    Amount         DECIMAL(18,2)  NOT NULL,
    PaymentMethod  NVARCHAR(100)  NOT NULL,
    PaymentDate    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    IsSuccessful   BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id)
);
GO

-- Начальные данные
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers (FirstName, LastName, Email, Phone, RegisteredAt) VALUES
    (N'Иван',  N'Петров',  N'ivan@mail.ru',  N'+375-29-123-45-67', DATEADD(DAY,-30, SYSUTCDATETIME())),
    (N'Мария', N'Иванова', N'maria@mail.ru', N'+375-29-765-43-21', DATEADD(DAY,-15, SYSUTCDATETIME()));
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products (Name, Description, Price, Stock, Category) VALUES
    (N'Ноутбук ASUS',        N'15.6", Intel Core i5, 8GB RAM',   1899.99, 1,  N'Электроника'),
    (N'Смартфон Samsung',    N'6.4", 128GB, 5G',                 1099.99, 25, N'Электроника'),
    (N'Наушники Sony',       N'Беспроводные, шумоподавление',     299.99, 50, N'Аксессуары'),
    (N'Книга C# в действии', N'Изучение C# на практике',           45.99, 30, N'Книги'),
    (N'Клавиатура Logitech', N'Механическая, RGB подсветка',      199.99, 15, N'Аксессуары');
END
GO


USE InternetShopDB_Primary;
GO
CREATE OR ALTER PROCEDURE dbo.ResetLabData
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Payments;
    DELETE FROM dbo.OrderItems;
    DELETE FROM dbo.Orders;
    DELETE FROM dbo.Products;
    DELETE FROM dbo.Customers;

    SET IDENTITY_INSERT dbo.Customers ON;

    INSERT INTO dbo.Customers (Id, FirstName, LastName, Email, Phone, RegisteredAt) VALUES
    (1, N'Иван',  N'Петров',  N'ivan@mail.ru',  N'+375-29-123-45-67', DATEADD(DAY,-30, SYSUTCDATETIME())),
    (2, N'Мария', N'Иванова', N'maria@mail.ru', N'+375-29-765-43-21', DATEADD(DAY,-15, SYSUTCDATETIME()));

    SET IDENTITY_INSERT dbo.Customers OFF;

    SET IDENTITY_INSERT dbo.Products ON;

    INSERT INTO dbo.Products (Id, Name, Description, Price, Stock, Category) VALUES
    (1, N'Ноутбук ASUS',        N'15.6", Intel Core i5, 8GB RAM',   1899.99, 1,  N'Электроника'),
    (2, N'Смартфон Samsung',    N'6.4", 128GB, 5G',                 1099.99, 25, N'Электроника'),
    (3, N'Наушники Sony',       N'Беспроводные, шумоподавление',     299.99, 50, N'Аксессуары'),
    (4, N'Книга C# в действии', N'Изучение C# на практике',           45.99, 30, N'Книги'),
    (5, N'Клавиатура Logitech', N'Механическая, RGB подсветка',      199.99, 15, N'Аксессуары');

    SET IDENTITY_INSERT dbo.Products OFF;

    DBCC CHECKIDENT ('dbo.Customers',  RESEED, 2);
    DBCC CHECKIDENT ('dbo.Products',   RESEED, 5);
    DBCC CHECKIDENT ('dbo.Orders',     RESEED, 0);
    DBCC CHECKIDENT ('dbo.OrderItems', RESEED, 0);
    DBCC CHECKIDENT ('dbo.Payments',   RESEED, 0);
END
GO

USE InternetShopDB_Replica;
GO
CREATE OR ALTER PROCEDURE dbo.ResetLabData
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Payments;
    DELETE FROM dbo.OrderItems;
    DELETE FROM dbo.Orders;
    DELETE FROM dbo.Products;
    DELETE FROM dbo.Customers;

    SET IDENTITY_INSERT dbo.Customers ON;

    INSERT INTO dbo.Customers (Id, FirstName, LastName, Email, Phone, RegisteredAt) VALUES
    (1, N'Иван',  N'Петров',  N'ivan@mail.ru',  N'+375-29-123-45-67', DATEADD(DAY,-30, SYSUTCDATETIME())),
    (2, N'Мария', N'Иванова', N'maria@mail.ru', N'+375-29-765-43-21', DATEADD(DAY,-15, SYSUTCDATETIME()));

    SET IDENTITY_INSERT dbo.Customers OFF;

    SET IDENTITY_INSERT dbo.Products ON;

    INSERT INTO dbo.Products (Id, Name, Description, Price, Stock, Category) VALUES
    (1, N'Ноутбук ASUS',        N'15.6", Intel Core i5, 8GB RAM',   1899.99, 1,  N'Электроника'),
    (2, N'Смартфон Samsung',    N'6.4", 128GB, 5G',                 1099.99, 25, N'Электроника'),
    (3, N'Наушники Sony',       N'Беспроводные, шумоподавление',     299.99, 50, N'Аксессуары'),
    (4, N'Книга C# в действии', N'Изучение C# на практике',           45.99, 30, N'Книги'),
    (5, N'Клавиатура Logitech', N'Механическая, RGB подсветка',      199.99, 15, N'Аксессуары');

    SET IDENTITY_INSERT dbo.Products OFF;

    DBCC CHECKIDENT ('dbo.Customers',  RESEED, 2);
    DBCC CHECKIDENT ('dbo.Products',   RESEED, 5);
    DBCC CHECKIDENT ('dbo.Orders',     RESEED, 0);
    DBCC CHECKIDENT ('dbo.OrderItems', RESEED, 0);
    DBCC CHECKIDENT ('dbo.Payments',   RESEED, 0);
END
GO