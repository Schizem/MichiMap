import { test, expect } from '@playwright/test';
import { mockApi, MOCK_EVENTS } from './helpers/mock-api';

// These tests verify the sidebar panel that opens when you click a map marker.
// Clicking a Leaflet SVG circle fires a click event that Angular's output() forwards
// to the parent App component, which sets selectedEvent() and renders the sidebar.

test.beforeEach(async ({ page }) => {
  await mockApi(page);
  await page.goto('/');
  await page.waitForSelector('.event-marker', { timeout: 10_000 });
});

test('sidebar is hidden on initial load', async ({ page }) => {
  await expect(page.locator('app-sidebar')).not.toBeAttached();
});

test('clicking a marker opens the sidebar', async ({ page }) => {
  await page.locator('.event-marker').first().click();
  await expect(page.locator('app-sidebar')).toBeVisible();
});

test('sidebar shows the event title from the clicked marker', async ({ page }) => {
  await page.locator('.event-marker').first().click();
  const expectedTitle = MOCK_EVENTS.features[0].properties.title;
  await expect(page.locator('.event-title')).toContainText(expectedTitle);
});

test('close button dismisses the sidebar', async ({ page }) => {
  await page.locator('.event-marker').first().click();
  await expect(page.locator('app-sidebar')).toBeVisible();

  await page.getByRole('button', { name: /close/i }).click();
  await expect(page.locator('app-sidebar')).not.toBeAttached();
});

test('sidebar shows severity badge when event has severity', async ({ page }) => {
  // The first mock event (FLOOD) has severity HIGH.
  await page.locator('.event-marker').first().click();
  await expect(page.locator('.severity-badge')).toBeVisible();
  await expect(page.locator('.severity-badge')).toContainText('HIGH');
});
