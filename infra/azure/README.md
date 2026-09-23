# Azure Infrastructure for Nordiska Sparbanken

Denna mapp innehaller Infrastructure as Code (IaC) definierad i Azure Bicep for att driftsatta Nordiska Sparbanken i Microsoft Azure (Sweden Central).

## Arkitekturkomponenter

1. **Azure Container Apps Environment & Container App:**
   - Multi-stage Docker-container (React 18 SPA + .NET 8 Web API).
   - Ingress pa port 8080 med automatisk TLS/HTTPS.
   - Skalning: minReplicas 1, maxReplicas 2.

2. **Azure Database for PostgreSQL Flexible Server:**
   - Burstable Standard_B1ms (32 GiB lagring).
   - Brandvaggsregel konfigurerad for intern Azure-trafik.

3. **Azure Container Registry (ACR):**
   - Privat Basic-tier container-register for byggen.
   - SystemAssigned Managed Identity med AcrPull-roll.

4. **Azure Log Analytics Workspace:**
   - Centraliserad insamling av applikations- och systemloggar.

## Deployment via Azure CLI

```bash
az group create --name rg-nordiska-prod --location swedencentral

az deployment group create \
  --resource-group rg-nordiska-prod \
  --template-file main.bicep \
  --parameters dbAdminPassword="<SecurePassword>" jwtSecretKey="<Min32CharsSecret>"
```
