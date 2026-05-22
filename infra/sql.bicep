// Azure SQL serverless tier, auto-pauses after 1 hour idle

param env string
param location string

@secure()
param sqlAdminPassword string = newGuid()

var serverName = 'michimap-sql-${env}'
var dbName = 'MichiMap'

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
    name: 'GP_S_Gen5_1'   // serverless, 1 vCore
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

resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output connectionString string = 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${dbName};Authentication=Active Directory Default;'
output serverName string = sqlServer.name
