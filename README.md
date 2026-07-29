# MichiMap (Currently Offline due to personal costs)

An interactive, real-time map of natural events across Michigan. The app pulls data from multiple state and federal APIs, normalizes them into a common format, and displays them as color-coded markers on a Leaflet map. Users can filter by event type, click markers to view details, and toggle between light and dark mode.

Built as a portfolio project targeting the Michigan DTMB developer opportunities.

**Live URL:** https://michimap.org

---

## What It Shows

| Event Type         | Color       | Data Source                     |
| ------------------ | ----------- | ------------------------------- |
| Flood warnings     | Blue        | NWS Alerts API                  |
| Active wildfires   | Deep orange | NASA FIRMS (satellite hotspots) |
| Controlled burns   | Orange      | Michigan DNR ArcGIS             |
| Air quality alerts | Purple      | EPA AirNow                      |
| Fish Atlas         | Green       | Michigan DNR Fish Atlas         |

---

## Data Sources and APIs

### NWS Alerts API

The National Weather Service publishes active alerts at `https://api.weather.gov/alerts/active`. MichiMap filters to Michigan (`area=MI`) and maps NWS severity levels (Extreme, Severe, Moderate, Minor) to an internal LOW/MODERATE/HIGH/CRITICAL scale. Alert geometries are polygons; the app computes the centroid for map placement.

### NASA FIRMS (US/Canada Feed)

The Fire Information for Resource Management System provides near real-time satellite fire detections at 375m resolution from the VIIRS instrument on Suomi NPP and NOAA-20 satellites. MichiMap fetches the Michigan bounding box (`-90.5,41.7,-82.1,48.3`) every two hours, skips low-confidence pixels, and maps Fire Radiative Power (FRP in megawatts) to severity. Requires a free MAP KEY from [firms.modaps.eosdis.nasa.gov](https://firms.modaps.eosdis.nasa.gov).

### EPA AirNow

The AirNow API provides hourly AQI readings by reporting area. MichiMap fetches all Michigan stations and only shows readings above AQI 100 (Unhealthy for Sensitive Groups or worse). Requires a free key from [airnowapi.org](https://docs.airnowapi.org/account/request/).

### Michigan DNR ArcGIS - Fire History (gis-michigan.opendata.arcgis.com)

The Michigan DNR publishes fire history through a verified ArcGIS Feature Service at `https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/pub_MiMorelsApp/FeatureServer/0`. MichiMap queries this layer twice daily, filtering by `Fire_Type='Wildfire'` for the wildfire fetcher and `Fire_Type='Prescribed Fire'` for the controlled burn fetcher. Key fields: `AcresBurned`, `County_Name`, `YearOccurred`. Coordinates are requested in EPSG:4326 via `outSR=4326`.

### Michigan DNR Fish Atlas (gis-michigan.opendata.arcgis.com)

The Michigan DNR Fish Atlas records verified species presence observations across Michigan waterbodies, published at `https://services3.arcgis.com/Jdnp1TjADvSDxMAX/arcgis/rest/services/DNRFisheriesDataOPENDATA/FeatureServer/0`. MichiMap fetches observations from 2020 onward (up to 1,000 records per run), showing where specific fish species have been confirmed present. Key fields: `CommonName`, `Taxon`, `Location`, `County`, `Year`, `GlobalID`.

---

## Architecture

```
Browser (Angular 21 / Leaflet)
        |
        | HTTPS
        v
Azure Static Web Apps
        |
        | /api/* proxy
        v
Azure App Service (ASP.NET Core 8 Web API)
        |
        | EF Core
        v
Azure SQL (serverless GP_S_Gen5_1, auto-pauses after 60 min idle)
        ^
        | upsert on schedule
Azure Functions (timer-triggered, .NET 8 isolated)
        |
        +-- NwsFetcherFunction       every 15 min
        +-- FirmsFetcherFunction     every 2 hours
        +-- EpaAirNowFetcherFunction every hour
        +-- DnrFetcherFunction       daily 06:00 UTC
        +-- NifcFetcherFunction      daily 06:00 UTC
        +-- DnrFishStockingFetcherFunction  daily 07:00 UTC
```

All fetchers use deterministic GUID generation (MD5 hash of the source system ID) so repeated runs are idempotent upserts rather than duplicate inserts.

---

## Project Structure

```
MichiMap/
├── src/
│   ├── MichiMap.Api/          ASP.NET Core 8 Web API
│   ├── MichiMap.Functions/    Azure Functions timer fetchers
│   ├── MichiMap.Tests/        xUnit + EF Core InMemory tests
│   └── MichiMap.Frontend/     Angular 21 app
│       └── e2e/               Playwright E2E + axe-core accessibility tests
├── infra/                     Azure Bicep templates
├── .azure-pipelines/          CI (ci.yml) and CD (cd.yml)
├── docs/                      Setup guides
└── TODO-USER.md               Items requiring manual action (API keys, Azure setup)
```

---

## Tech Stack

| Layer           | Technology                                            |
| --------------- | ----------------------------------------------------- |
| Frontend        | Angular 21, Leaflet.js, Angular Material              |
| API             | ASP.NET Core 8, EF Core 8, Swashbuckle                |
| Background jobs | Azure Functions v4 (.NET 8 isolated)                  |
| Database        | Azure SQL serverless                                  |
| Infrastructure  | Azure Bicep, Azure App Service, Azure Static Web Apps |
| CI/CD           | Azure Pipelines                                       |
| Testing         | xUnit, EF InMemory, Playwright, axe-core              |

---

## Local Development

### Prerequisites

| Tool       | Version                               |
| ---------- | ------------------------------------- |
| .NET 8 SDK | 8.x                                   |
| Node.js    | 20.x                                  |
| SQL Server | Developer Edition, LocalDB, or Docker |

See [docs/sql-server-local-setup.md](docs/sql-server-local-setup.md) for SQL Server setup options. The quickest path is Docker:

```powershell
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=YourPass123! -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

### 1. Start the API

```powershell
cd src/MichiMap.Api
dotnet ef database update       # creates the database and runs seed data
dotnet run                      # starts on http://localhost:5000
```

Swagger UI is available at `http://localhost:5000/swagger` in development.

### 2. Start the frontend

```powershell
cd src/MichiMap.Frontend
npm install
npm start                       # starts on http://localhost:4200
```

The dev server proxies `/api/*` to `localhost:5000` automatically via `proxy.conf.json`.

### 3. Run the fetchers locally (optional)

```powershell
cd src/MichiMap.Functions
# Copy local.settings.json.example to local.settings.json and fill in API keys
func start
```

---

## Running Tests

```powershell
# .NET unit tests (xUnit)
dotnet test MichiMap.sln

# Angular unit tests (Vitest)
cd src/MichiMap.Frontend
npm test

# Playwright E2E and accessibility tests
cd src/MichiMap.Frontend
npx playwright install chromium   # one-time
npm run test:e2e
```

---

## Deployment

Deployment is fully automated through Azure Pipelines.

Once the service connection and variable group are configured, every push to `main` triggers the CD pipeline which:

1. Provisions Azure infrastructure via Bicep (SQL Server, App Service, Function App, Storage, Static Web App)
2. Deploys the ASP.NET Core API to App Service
3. Deploys the Azure Functions
4. Builds the Angular app and deploys it to Azure Static Web Apps

The API runs EF migrations and seeds the database on first startup. No manual database setup is required.

To deploy manually via the Azure CLI:

```bash
az login
az group create --name michimap-rg --location eastus
az deployment group create \
  --resource-group michimap-rg \
  --template-file infra/main.bicep \
  --parameters env=prod sqlAdminPassword='YourPassword' epaAirNowApiKey='YourKey' firmsApiKey='YourKey'
```
