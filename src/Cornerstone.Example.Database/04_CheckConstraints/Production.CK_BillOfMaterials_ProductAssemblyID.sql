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
        check_constraints.name = 'CK_BillOfMaterials_ProductAssemblyID' AND
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
            check_constraints.name = 'CK_BillOfMaterials_ProductAssemblyID' AND
            schemas.name = 'Production' AND
            check_constraints.definition = '([ProductAssemblyID]<>[ComponentID])'
    )
BEGIN
ALTER TABLE [Production].[BillOfMaterials] DROP CONSTRAINT [CK_BillOfMaterials_ProductAssemblyID]
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
        check_constraints.name = 'CK_BillOfMaterials_ProductAssemblyID' AND
        schemas.name = 'Production'
)
ALTER TABLE [Production].[BillOfMaterials] WITH CHECK ADD CONSTRAINT [CK_BillOfMaterials_ProductAssemblyID] CHECK (([ProductAssemblyID]<>[ComponentID]))
GO
