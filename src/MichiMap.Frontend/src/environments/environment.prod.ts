export const environment = {
  production: true,
  // Full URL required in production - Azure Static Web Apps does not proxy /api.
  // The dev proxy (proxy.conf.json) handles this locally.
  apiBase: 'https://michimap-api-prod.azurewebsites.net/api'
};
