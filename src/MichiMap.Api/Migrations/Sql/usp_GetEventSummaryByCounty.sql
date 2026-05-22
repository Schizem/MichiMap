-- usp_GetEventSummaryByCounty
-- Returns active event counts grouped by county and event type.
-- Used by the frontend heatmap / county summary panel.
-- Raw SQL executed via IEventRepository for reporting queries (per spec section 4.3).

CREATE OR ALTER PROCEDURE [dbo].[usp_GetEventSummaryByCounty]
    @EventType VARCHAR(20) = NULL   -- optional filter; NULL returns all types
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.CountyFips,
        e.EventType,
        COUNT(*)            AS EventCount,
        MAX(e.FetchedAt)    AS LatestFetchedAt,
        -- Highest severity in each county/type bucket
        MAX(CASE e.Severity
                WHEN 'CRITICAL' THEN 4
                WHEN 'HIGH'     THEN 3
                WHEN 'MODERATE' THEN 2
                WHEN 'LOW'      THEN 1
                ELSE 0
            END)            AS MaxSeverityRank
    FROM dbo.NaturalEvents e
    WHERE
        e.IsDeleted = 0
        AND (e.ExpiresAt IS NULL OR e.ExpiresAt > GETUTCDATE())
        AND (@EventType IS NULL OR e.EventType = @EventType)
        AND e.CountyFips IS NOT NULL
    GROUP BY
        e.CountyFips,
        e.EventType
    ORDER BY
        MaxSeverityRank DESC,
        EventCount DESC;
END;
