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
        check_constraints.name = 'CK_SpecialOffer_MinQty' AND
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
            check_constraints.name = 'CK_SpecialOffer_MinQty' AND
            schemas.name = 'Sales' AND
            check_constraints.definition = '([MinQty]>=(0))'
    )
BEGIN
ALTER TABLE [Sales].[SpecialOffer] DROP CONSTRAINT [CK_SpecialOffer_MinQty]
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
        check_constraints.name = 'CK_SpecialOffer_MinQty' AND
        schemas.name = 'Sales'
)
ALTER TABLE [Sales].[SpecialOffer] WITH CHECK ADD CONSTRAINT [CK_SpecialOffer_MinQty] CHECK (([MinQty]>=(0)))
GO
