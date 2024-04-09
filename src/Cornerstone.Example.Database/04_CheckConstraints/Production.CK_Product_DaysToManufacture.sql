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
        check_constraints.name = 'CK_Product_DaysToManufacture' AND
        schemas.name = 'Production'
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
            check_constraints.name = 'CK_Product_DaysToManufacture' AND
            schemas.name = 'Production' AND
            check_constraints.definition = '([DaysToManufacture]>=(0))'
    )
BEGIN
ALTER TABLE [Production].[Product] DROP CONSTRAINT [CK_Product_DaysToManufacture]
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
        check_constraints.name = 'CK_Product_DaysToManufacture' AND
        schemas.name = 'Production'
)
ALTER TABLE [Production].[Product] WITH CHECK ADD CONSTRAINT [CK_Product_DaysToManufacture] CHECK (([DaysToManufacture]>=(0)))
GO
