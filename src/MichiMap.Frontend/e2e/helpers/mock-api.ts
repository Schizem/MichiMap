import { Page } from '@playwright/test';

// Two representative events used across all test files.
// Wayne County (FLOOD) and Manistee County (MOREL) give us two distinct marker types.
export const MOCK_EVENTS = {
  type: 'FeatureCollection',
  features: [
    {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [-83.05, 42.33] },
      properties: {
        eventId: '00000000-0000-0000-0000-000000000001',
        eventType: 'FLOOD',
        title: 'Flood Warning - Wayne County',
        description: 'River levels exceeding flood stage along the Rouge River.',
        severity: 'HIGH',
        countyFips: '26163',
        sourceUrl: 'https://www.weather.gov/',
        fetchedAt: new Date().toISOString(),
        expiresAt: null,
        photoUrl: null,
      },
    },
    {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [-86.01, 44.25] },
      properties: {
        eventId: '00000000-0000-0000-0000-000000000002',
        eventType: 'MOREL',
        title: 'Morel Sighting - Manistee County',
        description: 'Spotted near a recent burn area.',
        severity: null,
        countyFips: '26101',
        sourceUrl: null,
        fetchedAt: new Date().toISOString(),
        expiresAt: null,
        photoUrl: null,
      },
    },
  ],
};

// A small list of Michigan counties for the form dropdown.
export const MOCK_COUNTIES = [
  'Alcona',
  'Alger',
  'Allegan',
  'Alpena',
  'Antrim',
  'Manistee',
  'Mason',
  'Mecosta',
  'Wayne',
  'Wexford',
];

// Intercepts all API calls the Angular app makes and returns mock data.
export async function mockApi(page: Page): Promise<void> {
  await page.route('**/api/events**', (route) => route.fulfill({ json: MOCK_EVENTS }));
  await page.route('**/api/submissions/counties', (route) =>
    route.fulfill({ json: MOCK_COUNTIES }),
  );
  await page.route('**/api/submissions/morel', (route) =>
    route.fulfill({ status: 201, json: { id: '00000000-0000-0000-0000-000000000099' } }),
  );
}
