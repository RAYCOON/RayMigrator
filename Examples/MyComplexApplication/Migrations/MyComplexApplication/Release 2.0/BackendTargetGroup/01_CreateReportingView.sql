/*
[RayMigrator]
Description = "Create MonthlyTransactionSummary view for reporting"
*/

CREATE VIEW [dbo].[MonthlyTransactionSummary]
AS
SELECT
    a.[AccountNumber],
    a.[HolderName],
    YEAR(t.[TransactionDate])  AS [Year],
    MONTH(t.[TransactionDate]) AS [Month],
    COUNT(*)                   AS [TransactionCount],
    SUM(CASE WHEN t.[Amount] > 0 THEN t.[Amount] ELSE 0 END)      AS [TotalCredits],
    SUM(CASE WHEN t.[Amount] < 0 THEN ABS(t.[Amount]) ELSE 0 END) AS [TotalDebits]
FROM [dbo].[Account] a
INNER JOIN [dbo].[Transaction] t ON a.[Id] = t.[AccountId]
GROUP BY
    a.[AccountNumber],
    a.[HolderName],
    YEAR(t.[TransactionDate]),
    MONTH(t.[TransactionDate])
GO
