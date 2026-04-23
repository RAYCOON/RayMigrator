/*
[RayMigrator]
Description = "Production-specific: Add audit logging"
Environments = ["Production"]
*/

-- This migration only runs in Production environment
-- Environment-specific migrations use the naming pattern:
-- {Sequence}_{Description}.{Environment}.sql

CREATE TABLE AuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(128) NOT NULL,
    RecordId INT NOT NULL,
    Action NVARCHAR(10) NOT NULL,  -- INSERT, UPDATE, DELETE
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    ChangedBy NVARCHAR(100) NOT NULL,
    ChangedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_AuditLog_TableName ON AuditLog(TableName, RecordId);
CREATE INDEX IX_AuditLog_ChangedAt ON AuditLog(ChangedAt DESC);
