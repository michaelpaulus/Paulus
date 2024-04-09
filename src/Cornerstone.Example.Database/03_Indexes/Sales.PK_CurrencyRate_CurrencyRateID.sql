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
            indexes.name = 'PK_CurrencyRate_CurrencyRateID' AND
            schemas.name = 'Sales'
    )
    ALTER TABLE [Sales].[CurrencyRate] ADD CONSTRAINT [PK_CurrencyRate_CurrencyRateID] PRIMARY KEY CLUSTERED
    (
    [CurrencyRateID]
    )
GO
