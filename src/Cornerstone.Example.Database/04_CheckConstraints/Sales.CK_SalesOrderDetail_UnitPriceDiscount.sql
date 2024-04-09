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
        check_constraints.name = 'CK_SalesOrderDetail_UnitPriceDiscount' AND
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
            check_constraints.name = 'CK_SalesOrderDetail_UnitPriceDiscount' AND
            schemas.name = 'Sales' AND
            check_constraints.definition = '([UnitPriceDiscount]>=(0.00))'
    )
BEGIN
ALTER TABLE [Sales].[SalesOrderDetail] DROP CONSTRAINT [CK_SalesOrderDetail_UnitPriceDiscount]
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
        check_constraints.name = 'CK_SalesOrderDetail_UnitPriceDiscount' AND
        schemas.name = 'Sales'
)
ALTER TABLE [Sales].[SalesOrderDetail] WITH CHECK ADD CONSTRAINT [CK_SalesOrderDetail_UnitPriceDiscount] CHECK (([UnitPriceDiscount]>=(0.00)))
GO
