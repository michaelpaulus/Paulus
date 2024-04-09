﻿
IF EXISTS
(
    SELECT
        1
    FROM
        sys.check_constraints INNER JOIN
        sys.tables ON
            check_constraints.parent_object_id = tables.object_id INNER JOIN
        sys.schemas ON
            tables.schema_id = schemas.schema_id
    WHERE
        check_constraints.name = 'CK_SalesPerson_SalesLastYear' AND
        schemas.name = 'Sales'
)
BEGIN
IF NOT EXISTS
    (
        SELECT
            1
        FROM
            sys.check_constraints INNER JOIN
            sys.tables ON
                check_constraints.parent_object_id = tables.object_id INNER JOIN
            sys.schemas ON
                tables.schema_id = schemas.schema_id
        WHERE
            check_constraints.name = 'CK_SalesPerson_SalesLastYear' AND
            schemas.name = 'Sales' AND
            check_constraints.definition = '([SalesLastYear]>=(0.00))'
    )
BEGIN
ALTER TABLE [Sales].[SalesPerson] DROP CONSTRAINT [CK_SalesPerson_SalesLastYear]
END
END
GO

IF NOT EXISTS
(
    SELECT
        1
    FROM
        sys.check_constraints INNER JOIN
        sys.tables ON
            check_constraints.parent_object_id = tables.object_id INNER JOIN
        sys.schemas ON
            tables.schema_id = schemas.schema_id
    WHERE
        check_constraints.name = 'CK_SalesPerson_SalesLastYear' AND
        schemas.name = 'Sales'
)
ALTER TABLE [Sales].[SalesPerson] WITH CHECK ADD CONSTRAINT [CK_SalesPerson_SalesLastYear] CHECK (([SalesLastYear]>=(0.00)))
GO
