USE InternetShopDB_Primary;
GO
EXEC dbo.ResetLabData;
GO

USE InternetShopDB_Replica;
GO
EXEC dbo.ResetLabData;
GO