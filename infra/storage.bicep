// Azure Blob Storage for morel photo uploads and future API snapshots

param env string
param location string

// Storage account names must be globally unique, lowercase, 3-24 chars.
var storageAccountName = 'michimapstor${env}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource photosContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'morel-photos'
  properties: {
    publicAccess: 'None'
  }
}

// Azure Functions requires a storage account connection for internal state management.
// Outputting the connection string here so appservice.bicep can pass it to the Function App.
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
output storageAccountName string = storageAccount.name
