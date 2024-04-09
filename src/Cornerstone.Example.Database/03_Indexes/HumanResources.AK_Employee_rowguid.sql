﻿IF NOT EXISTS
    (
        SELECT
            1
        FROM
            sys.indexes INNER JOIN
            sys.tables ON
                indexes.object_id = tables.object_id INNER JOIN
            sys.schemas ON
                tables.schema_id = schemas.schema_id
        WHERE
            indexes.name = 'AK_Employee_rowguid' AND
            schemas.name = 'HumanResources'
    )
    CREATE UNIQUE NONCLUSTERED INDEX [AK_Employee_rowguid] ON [HumanResources].[Employee]
    (
    [rowguid]
    )
GO
