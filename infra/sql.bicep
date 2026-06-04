// Azure SQL serverless tier, auto-pauses after 1 hour idle

param env string
param location string

// Passed in from the pipeline as a secret variable; never generated here
// because newGuid() changes each deploy and you'd lose track of the password.
@secure()
param sqlAdminPassword string

var serverName = 'michimap-sql-${env}'
var dbName     = 'MichiMap'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: 'michimapadmin'
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: dbName
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: '0.5'
    zoneRedundant: false
  }
}

// Allow Azure services (includes App Service, Functions, and pipeline agents).
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Standard SQL auth connection string - works without Managed Identity setup.
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${dbName};Persist Security Info=False;User ID=michimapadmin;Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
output serverName string = sqlServer.name
