// App Service F1 (free) for ASP.NET Core Web API
// Upgrade to B1 (~$13/mo) before a live demo — F1 is CPU-throttled
// AWS analogue: Elastic Beanstalk or App Runner

param env string
param location string
param sqlConnectionString string

var planName = 'michimap-plan-${env}'
var apiName = 'michimap-api-${env}'
var funcName = 'michimap-func-${env}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: 'F1'   // free tier; switch to B1 for demo
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
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: env == 'prod' ? 'Production' : 'Development'
        }
      ]
    }
  }
}

// Static Web App for Angular frontend (free tier)
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'michimap-frontend-${env}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: ''     // TODO: set to Azure DevOps repo URL
    branch: env == 'prod' ? 'main' : 'dev'
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output frontendUrl string = 'https://${staticWebApp.properties.defaultHostname}'
