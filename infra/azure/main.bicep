targetScope = 'resourceGroup'

@description('Azure region for resources (e.g. swedencentral, westeurope, italynorth)')
param location string = resourceGroup().location

@description('Base name for the application resources')
param appName string = 'nordiska-sparbanken'

@description('Name for the Container Apps Environment')
param environmentName string = 'env-${appName}'

@description('Name for the Azure Container Registry (must be globally unique, alphanumeric)')
param acrName string = 'acrnordiska${uniqueString(resourceGroup().id)}'

@description('PostgreSQL server administrator username')
param dbAdminUsername string = 'nordiska_admin'

@description('PostgreSQL server administrator password')
@secure()
param dbAdminPassword string

@description('JWT secret key for signing customer session tokens (min 32 chars)')
@secure()
param jwtSecretKey string

@description('Initial container image to deploy (default: placeholder image until first CI/CD build)')
param containerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// ---------------------------------------------------------------------------
// 1. Log Analytics Workspace (Required for Container Apps Environment)
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${appName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
// 2. Azure Container Registry (ACR) - Basic Tier
// ---------------------------------------------------------------------------
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// ---------------------------------------------------------------------------
// 3. Azure Database for PostgreSQL Flexible Server (Burstable B1ms Free Tier)
// ---------------------------------------------------------------------------
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: 'psql-${appName}-${uniqueString(resourceGroup().id)}'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: dbAdminUsername
    administratorLoginPassword: dbAdminPassword
    version: '15'
    storage: {
      storageSizeGB: 32
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

// Allow connections from Azure internal services (Container Apps)
resource allowAzureIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-03-01-preview' = {
  parent: postgres
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Provision application database
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgres
  name: 'nordiska_db'
}

// ---------------------------------------------------------------------------
// 4. Azure Container Apps Managed Environment
// ---------------------------------------------------------------------------
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
// 5. Azure Container App (React Frontend SPA + .NET 8 Web API)
// ---------------------------------------------------------------------------
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'db-connection'
          value: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=nordiska_db;Username=${dbAdminUsername};Password=${dbAdminPassword};SSL Mode=Require;'
        }
        {
          name: 'jwt-secret'
          value: jwtSecretKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'nordiska-app'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ConnectionStrings__BankingDb'
              secretRef: 'db-connection'
            }
            {
              name: 'ConnectionStrings__FaqDb'
              secretRef: 'db-connection'
            }
            {
              name: 'ConnectionStrings__ReportingDb'
              secretRef: 'db-connection'
            }
            {
              name: 'Jwt__SecretKey'
              secretRef: 'jwt-secret'
            }
            {
              name: 'Jwt__Issuer'
              value: 'NordiskaSparbanken'
            }
            {
              name: 'Jwt__Audience'
              value: 'NordiskaSparbankenPortal'
            }
            {
              name: 'Jwt__TokenLifetimeInMinutes'
              value: '15'
            }
            {
              name: 'ActiveLogin__BankId__Environment'
              value: 'Simulated'
            }
            {
              name: 'Banking__PlannedWorkerIntervalSeconds'
              value: '60'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------
// 6. RBAC Role Assignment: Grant AcrPull to Container App Managed Identity
// ---------------------------------------------------------------------------
var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ef3-4680-a075-32614d47b0d4')

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerApp.id, acrPullRoleDefinitionId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Public HTTPS URL to access the Nordiska Sparbanken portal')
output applicationUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('Login server for Azure Container Registry')
output acrLoginServer string = acr.properties.loginServer

@description('Fully Qualified Domain Name of PostgreSQL Flexible Server')
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName

@description('Name of the provisioned Azure Container App')
output containerAppName string = containerApp.name