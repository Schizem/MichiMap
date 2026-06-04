// App Service (API) + Azure Functions (data fetchers) + Static Web App (Angular frontend)
// App Service F1 is free but CPU-throttled. Upgrade to B1 (~$13/mo) before a live demo.

param env string
param location string
param sqlConnectionString string
param storageConnectionString string

@secure()
param epaAirNowApiKey string = ''

@secure()
param firmsApiKey string = ''

var planName    = 'michimap-plan-${env}'
var apiName     = 'michimap-api-${env}'
var funcName    = 'michimap-func-${env}'
var funcPlanName = 'michimap-func-plan-${env}'

// App Service Plan for the ASP.NET Core API (F1 free tier).
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
}

resource apiApp 'Microsoft.Web/sites@2023-01-01' = {
  name: apiName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: env == 'prod' ? 'Production' : 'Development'
        }
        {
          // Comma-separated list; Program.cs splits on commas so both domains are allowed.
          name: 'AllowedOrigins'
          value: 'https://michimap.org,https://www.michimap.org'
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

// Consumption plan for Azure Functions (serverless, free up to 1M executions/month).
// Timer-triggered functions are a natural fit for the serverless model.
resource funcConsumptionPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: funcPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: funcName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: funcConsumptionPlan.id
    siteConfig: {
      // .NET 8 isolated worker model
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          // Azure Functions requires a storage account for internal state and lease management.
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'SqlConnectionString'
          value: sqlConnectionString
        }
        {
          name: 'EpaAirNowApiKey'
          value: epaAirNowApiKey
        }
        {
          name: 'FirmsApiKey'
          value: firmsApiKey
        }
        {
          name: 'AzureBlobStorage'
          value: storageConnectionString
        }
      ]
    }
  }
}

// Static Web App for the Angular frontend (free tier).
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'michimap-frontend-${env}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output apiUrl string         = 'https://${apiApp.properties.defaultHostName}'
output frontendUrl string    = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppName string = staticWebApp.name
