/*
    Crea, oppure adegua, il campo mo_hhnumvoce su MOVOFF e MOVORD.

    Lo script è rilanciabile:
    - aggiunge il campo se non esiste;
    - elimina l'eventuale vincolo DEFAULT;
    - imposta varchar(50) NULL se il campo esiste già.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.movoff', N'U') IS NULL
        THROW 50001, 'Tabella dbo.movoff non trovata.', 1;

    IF OBJECT_ID(N'dbo.movord', N'U') IS NULL
        THROW 50002, 'Tabella dbo.movord non trovata.', 1;

    IF COL_LENGTH(N'dbo.movoff', N'mo_hhnumvoce') IS NULL
    BEGIN
        ALTER TABLE dbo.movoff
            ADD mo_hhnumvoce varchar(50) NULL;
    END
    ELSE
    BEGIN
        DECLARE @DefaultMovoff sysname;

        SELECT @DefaultMovoff = dc.name
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.movoff')
          AND c.name = N'mo_hhnumvoce';

        IF @DefaultMovoff IS NOT NULL
            EXEC
            (
                N'ALTER TABLE dbo.movoff DROP CONSTRAINT '
                + QUOTENAME(@DefaultMovoff) + N';'
            );

        ALTER TABLE dbo.movoff
            ALTER COLUMN mo_hhnumvoce varchar(50) NULL;
    END;

    IF COL_LENGTH(N'dbo.movord', N'mo_hhnumvoce') IS NULL
    BEGIN
        ALTER TABLE dbo.movord
            ADD mo_hhnumvoce varchar(50) NULL;
    END
    ELSE
    BEGIN
        DECLARE @DefaultMovord sysname;

        SELECT @DefaultMovord = dc.name
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.movord')
          AND c.name = N'mo_hhnumvoce';

        IF @DefaultMovord IS NOT NULL
            EXEC
            (
                N'ALTER TABLE dbo.movord DROP CONSTRAINT '
                + QUOTENAME(@DefaultMovord) + N';'
            );

        ALTER TABLE dbo.movord
            ALTER COLUMN mo_hhnumvoce varchar(50) NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT
    OBJECT_SCHEMA_NAME(c.object_id) AS schema_name,
    OBJECT_NAME(c.object_id) AS table_name,
    c.name AS column_name,
    TYPE_NAME(c.user_type_id) AS data_type,
    c.max_length,
    c.is_nullable
FROM sys.columns AS c
WHERE c.object_id IN
(
    OBJECT_ID(N'dbo.movoff'),
    OBJECT_ID(N'dbo.movord')
)
AND c.name = N'mo_hhnumvoce'
ORDER BY table_name;
