import { Page } from '@playwright/test';

// Two representative events used across all test files.
export const MOCK_EVENTS = {
  type: 'FeatureCollection',
  features: [
    {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [-83.05, 42.33] },
      properties: {
        eventId:    '00000000-0000-0000-0000-000000000001',
        eventType:  'FLOOD',
        title:      'Flood Warning - Wayne County',
        description: 'River levels exceeding flood stage along the Rouge River.',
        severity:   'HIGH',
        countyFips: '26163',
        sourceUrl:  'https://www.weather.gov/',
        fetchedAt:  new Date().toISOString(),
        expiresAt:  null,
      },
    },
    {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [-86.01, 44.25] },
      properties: {
        eventId:    '00000000-0000-0000-0000-000000000002',
        eventType:  'WILDFIRE',
        title:      'Active Fire Detection - Manistee County',
        description: 'Satellite detection via NASA FIRMS.',
        severity:   'MODERATE',
        countyFips: '26101',
        sourceUrl:  'https://firms.modaps.eosdis.nasa.gov/usfs/',
        fetchedAt:  new Date().toISOString(),
        expiresAt:  null,
      },
    },
  ],
};

// Intercepts all API calls the Angular app makes and returns mock data.
export async function mockApi(page: Page): Promise<void> {
  await page.route('**/api/events**', (route) => route.fulfill({ json: MOCK_EVENTS }));
}
