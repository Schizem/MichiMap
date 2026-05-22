// MichiMap — Azure infrastructure entry point

@description('Environment name (dev, prod)')
param env string = 'dev'

@description('Azure region')
param location string = resourceGroup().location

module sql 'sql.bicep' = {
  name: 'sql'
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
    sqlConnectionString: sql.outputs.connectionString
  }
}

module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    env: env
    location: location
  }
}
