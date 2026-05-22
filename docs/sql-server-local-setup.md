# SQL Server Local Setup

`dotnet ef database update` and `dotnet run` require a reachable SQL Server instance.
Pick whichever option is easiest for your machine.

---

## Option A — SQL Server Developer Edition (recommended for long-term dev)

Free, full-featured, Windows native. No Docker needed.

1. Download from https://www.microsoft.com/en-us/sql-server/sql-server-downloads (Developer edition)
2. Install with default instance name `MSSQLSERVER`
3. Update `appsettings.json` connection string:

```json
"DefaultConnection": "Server=localhost;Database=MichiMap;Integrated Security=True;TrustServerCertificate=True;"
```

4. Run migrations: `dotnet ef database update --project src/MichiMap.Api`

---

## Option B — SQL Server Express LocalDB (lightest option)

Installs alongside VS 2022 or as a standalone download.

Standalone download: https://www.microsoft.com/en-us/sql-server/sql-server-downloads → Express → LocalDB

Default connection string (already in `appsettings.json`):
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MichiMap;Trusted_Connection=True;"
```

Verify LocalDB is running: `SqlLocalDB info`

---

## Option C — Docker (no Windows install, isolated)

Requires Docker Desktop: https://www.docker.com/products/docker-desktop/

```powershell
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=MichiMap_Dev1!" `
  -p 1433:1433 --name michimap-sql -d `
  mcr.microsoft.com/mssql/server:2022-latest
```

Update `appsettings.Development.json` (gitignored — safe for passwords):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=MichiMap;User Id=sa;Password=MichiMap_Dev1!;TrustServerCertificate=True;"
  }
}
```

Run migrations: `dotnet ef database update --project src/MichiMap.Api`

---

## After database is running

```powershell
# Apply schema + run seeder
dotnet ef database update --project src/MichiMap.Api

# Run the stored procedure (after first migration)
# Connect via SSMS or Azure Data Studio and run:
# src/MichiMap.Api/Migrations/Sql/usp_GetEventSummaryByCounty.sql

# Start the API (seed data loads automatically in Development mode)
dotnet run --project src/MichiMap.Api
# Swagger: https://localhost:5001/swagger
```
