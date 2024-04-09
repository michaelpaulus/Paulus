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
        check_constraints.name = 'CK_Employee_MaritalStatus' AND
        schemas.name = 'HumanResources'
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
            check_constraints.name = 'CK_Employee_MaritalStatus' AND
            schemas.name = 'HumanResources' AND
            check_constraints.definition = '(upper([MaritalStatus])=''S'' OR upper([MaritalStatus])=''M'')'
    )
BEGIN
ALTER TABLE [HumanResources].[Employee] DROP CONSTRAINT [CK_Employee_MaritalStatus]
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
        check_constraints.name = 'CK_Employee_MaritalStatus' AND
        schemas.name = 'HumanResources'
)
ALTER TABLE [HumanResources].[Employee] WITH CHECK ADD CONSTRAINT [CK_Employee_MaritalStatus] CHECK ((upper([MaritalStatus])='S' OR upper([MaritalStatus])='M'))
GO
