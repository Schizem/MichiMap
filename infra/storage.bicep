// Azure Blob Storage, cached API snapshots + morel photo uploads

param env string
param location string

var storageAccountName = 'michimapstor${env}'   // must be globally unique, lowercase, 3-24 chars

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'   // locally redundant — cheapest option
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

resource snapshotsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'snapshots'
  properties: {
    publicAccess: 'None'
  }
}

resource photosContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'morel-photos'
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = storageAccount.name
