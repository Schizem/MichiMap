import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { mockApi } from './helpers/mock-api';

// These tests verify the map renders correctly and passes accessibility checks.
// axe-core scans the live DOM for WCAG violations and catches things like missing
// labels, low contrast, and keyboard traps that are hard to catch with unit tests.

test.beforeEach(async ({ page }) => {
  await mockApi(page);
  await page.goto('/');
  // Wait for the Leaflet tile layer to initialize before asserting anything.
  await page.waitForSelector('.leaflet-container', { timeout: 10_000 });
});

test('shows the app toolbar with the MichiMap title', async ({ page }) => {
  await expect(page.locator('mat-toolbar')).toContainText('MichiMap');
});

test('renders the Leaflet map container', async ({ page }) => {
  await expect(page.locator('.leaflet-container')).toBeVisible();
});

test('shows all seven event type chips in the legend', async ({ page }) => {
  const chips = page.locator('.legend-chip');
  await expect(chips).toHaveCount(7);
});

test('renders a marker for each mock event', async ({ page }) => {
  // Circle markers get the .event-marker class added in map.component.ts.
  // We expect exactly two markers; one for each item in MOCK_EVENTS.
  await expect(page.locator('.event-marker')).toHaveCount(2);
});

test('shows the Report Sighting button', async ({ page }) => {
  await expect(page.getByRole('button', { name: /report sighting/i })).toBeVisible();
});

test('page has no critical accessibility violations', async ({ page }) => {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa'])
    .exclude('.leaflet-tile-pane')
    .analyze();

  expect(results.violations).toEqual([]);
});
