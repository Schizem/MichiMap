# MichiMap — Michigan Natural Events Map

Interactive, real-time map of Michigan's natural events (floods, wildfires, air quality alerts, beach closures, fish stocking, morel sightings, and more).

**Stack:** Angular 21 · ASP.NET Core 8 · Azure SQL · Azure Functions · Azure Pipelines · Leaflet.js

Built to demonstrate the full Michigan DTMB stack.

---

## Project Structure

```
michimap/
├── src/
│   ├── MichiMap.Api/          # ASP.NET Core 8 Web API (C#)
│   ├── MichiMap.Functions/    # Azure Functions timer fetchers (C#)
│   ├── MichiMap.Tests/        # xUnit + EF Core InMemory tests
│   └── MichiMap.Frontend/     # Angular 21 app (Leaflet, Angular Material)
│       └── e2e/               # Playwright E2E + axe-core ADA checks
├── infra/                     # Azure Bicep templates
├── .azure-pipelines/          # CI (ci.yml) and CD (cd.yml)
└── MichiMap.sln
```

---

## Prerequisites

| Tool                                                           | Version | Purpose                       |
| -------------------------------------------------------------- | ------- | ----------------------------- |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.x     | API, Functions, Tests         |
| Node.js                                                        | 20.x    | Angular frontend              |
| Angular CLI                                                    | 21.x    | `npm install -g @angular/cli` |
| Azure CLI                                                      | latest  | Provisioning & deployment     |
| Azure Functions Core Tools                                     | 4.x     | Run Functions locally         |

---

## Local Development

### 1. API (ASP.NET Core)

```powershell
# Requires .NET 8 SDK + SQL Server LocalDB (included with VS 2022)
cd src/MichiMap.Api
dotnet ef database update       # apply EF Core migrations
dotnet run                      # starts on https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
```

### 2. Angular Frontend

```powershell
cd src/MichiMap.Frontend
npm install
ng serve                        # starts on http://localhost:4200
```

### 3. Azure Functions (fetchers)

```powershell
cd src/MichiMap.Functions
# copy local.settings.json.example to local.settings.json and fill in connection strings
func start
```

---

## Running Tests

```powershell
# xUnit (C# unit tests)
dotnet test MichiMap.sln

# Jasmine/Karma (Angular unit tests)
cd src/MichiMap.Frontend && ng test --watch=false

# Playwright (E2E + ADA)
cd src/MichiMap.Frontend && npx playwright test
```

---

## Azure Deployment

```powershell
# One-time: create resource group and provision infrastructure
az login
az group create --name michimap-rg --location eastus
az deployment group create --resource-group michimap-rg --template-file infra/main.bicep

# CI/CD is handled by Azure Pipelines (.azure-pipelines/ci.yml and cd.yml)
```

---

## Development Milestones

| Phase | Milestone                                                     | Status  |
| ----- | ------------------------------------------------------------- | ------- |
| 0     | Scaffold, folder structure, Bicep stubs, pipeline skeletons   | ✅ Done |
| 1     | Azure SQL schema, EF Core models, migrations, seed data       | ⬜      |
| 2     | ASP.NET Core Web API — events + submission endpoints          | ⬜      |
| 3     | Azure Functions fetchers (NWS, NIFC, EPA, DNR, Fish Stocking) | ⬜      |
| 4     | Angular map shell, Leaflet markers, layer toggles, sidebar    | ⬜      |
| 5     | Morel submission form, rate limiting, fuzzy location          | ⬜      |
| 6     | Jasmine/Karma tests, Playwright E2E, axe-core ADA             | ⬜      |
| 7     | Full CI/CD, Application Insights, custom domain               | ⬜      |
