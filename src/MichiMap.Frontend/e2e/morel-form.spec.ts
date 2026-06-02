import { test, expect } from '@playwright/test';
import { mockApi, MOCK_COUNTIES } from './helpers/mock-api';

// These tests cover the morel submission dialog end-to-end.
// The mock API intercepts POST /api/submissions/morel and returns a 201 so the
// test suite works without a running backend.

test.beforeEach(async ({ page }) => {
  await mockApi(page);
  await page.goto('/');
  await page.waitForSelector('mat-toolbar', { timeout: 10_000 });
});

test('Report Sighting button opens the dialog', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();
  await expect(page.locator('app-morel-form')).toBeVisible();
});

test('dialog title reads "Report a Morel Sighting"', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();
  await expect(page.locator('mat-dialog-container')).toContainText('Report a Morel Sighting');
});

test('county dropdown is populated from the API', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();

  // Open the Material select to reveal the options.
  await page.locator('mat-select').click();
  const firstOption = MOCK_COUNTIES[0];
  await expect(page.getByRole('option', { name: firstOption })).toBeVisible();
});

test('touching a required field without filling it shows a validation error', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();

  // Open the county dropdown and close it without selecting anything.
  // Angular Material marks the control as "touched" when the panel closes,
  // which triggers the required validator to show mat-error.
  await page.locator('mat-select').click();
  await page.keyboard.press('Escape');

  await expect(page.locator('mat-error')).toBeVisible();
});

test('cancel button closes the dialog without submitting', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();
  await expect(page.locator('mat-dialog-container')).toBeVisible();

  await page.getByRole('button', { name: /cancel/i }).click();
  await expect(page.locator('mat-dialog-container')).not.toBeAttached();
});

test('valid submission closes the dialog', async ({ page }) => {
  await page.getByRole('button', { name: /report sighting/i }).click();

  // Fill in county.
  await page.locator('mat-select').click();
  await page.getByRole('option', { name: 'Wayne' }).click();

  // Fill in date using the text input directly (faster than navigating the calendar).
  await page.locator('input[matinput]').fill('6/1/2026');
  await page.keyboard.press('Tab');

  await page.getByRole('button', { name: /^submit$/i }).click();

  // On success the dialog closes and the mock API response triggers map reload.
  await expect(page.locator('mat-dialog-container')).not.toBeAttached({ timeout: 5_000 });
});
