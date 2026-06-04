// MichiMap infrastructure entry point
// Deploy with: az deployment group create --resource-group michimap-rg --template-file infra/main.bicep --parameters env=prod sqlAdminPassword='...' epaAirNowApiKey='...' firmsApiKey='...'

@description('Environment name (dev or prod)')
param env string = 'dev'

@description('Azure region')
param location string = resourceGroup().location

@secure()
param sqlAdminPassword string

@secure()
param epaAirNowApiKey string = ''

@secure()
param firmsApiKey string = ''

module sql 'sql.bicep' = {
  name: 'sql'
  params: {
    env: env
    location: location
    sqlAdminPassword: sqlAdminPassword
  }
}

module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    env: env
    location: location
  }
}

module appService 'appservice.bicep' = {
  name: 'appservice'
  params: {
    env: env
    location: location
    sqlConnectionString:     sql.outputs.connectionString
    storageConnectionString: storage.outputs.connectionString
    epaAirNowApiKey:         epaAirNowApiKey
    firmsApiKey:             firmsApiKey
  }
}

output apiUrl string      = appService.outputs.apiUrl
output frontendUrl string = appService.outputs.frontendUrl
