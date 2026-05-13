import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE_URL = 'http://localhost:5173';
const USER_EMAIL = 'qa.superadmin@local.test';
const USER_PASSWORD = 'QaPassword123!';

const artifactsDir = path.resolve('e2e', 'artifacts', 'manual-verify');
fs.mkdirSync(artifactsDir, { recursive: true });

async function fail(page, step, details) {
  const shot = path.join(artifactsDir, `FAIL-${step.replace(/[^a-z0-9]+/gi, '_')}.png`);
  await page.screenshot({ path: shot, fullPage: true });
  console.log(JSON.stringify({ status: 'failed', step, details, screenshot: shot }, null, 2));
  process.exit(2);
}

async function run() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  const page = await context.newPage();
  try {
    await page.goto(`${BASE_URL}/login`, { waitUntil: 'domcontentloaded' });
    await page.fill('#email', USER_EMAIL);
    await page.fill('#password', USER_PASSWORD);
    await page.getByRole('button', { name: /Iniciar sesi[oó]n/i }).click();
    await page.waitForLoadState('networkidle');
    await page.goto(`${BASE_URL}/superadmin/menu-plans`, { waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle');
    const openBuilderTab = async () => {
      await page.getByRole('tab', { name: /Constructor de Men[uú]s/i }).click();
      await page.waitForLoadState('networkidle');
    };

    // external tabs
    for (const tabName of ['Constructor de Menús', 'Planes y Suscripciones', 'Auditoría Global']) {
      if (!(await page.getByRole('tab', { name: new RegExp(tabName, 'i') }).isVisible())) {
        await fail(page, 'tabs-externas', `No se encontró tab externa: ${tabName}`);
      }
    }

    // internal plan tabs
    for (const code of ['STARTER', 'BUSINESS', 'PROFESSIONAL', 'ENTERPRISE']) {
      if (!(await page.getByRole('tab', { name: new RegExp(code, 'i') }).isVisible())) {
        await fail(page, 'tabs-planes', `No se encontró tab de plan: ${code}`);
      }
    }

    // initial tree roots
    const allNames = (await page.locator('.menu-builder-crm-name').allTextContents()).join(' | ');
    for (const root of ['Inventario', 'Ventas', 'Compras']) {
      if (!allNames.toLowerCase().includes(root.toLowerCase())) {
        await fail(page, 'arbol-inicial', `No aparece el nodo raíz esperado "${root}"`);
      }
    }

    // collapse / expand
    const invRow = page.locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: /Inventario/i }) }).first();
    const toggleBtn = invRow.locator('button[title="Expandir"],button[title="Colapsar"]').first();
    const beforeText = await toggleBtn.textContent();
    await toggleBtn.click();
    const afterText = await toggleBtn.textContent();
    if (beforeText === afterText) {
      await fail(page, 'expand-collapse', 'El botón de expandir/colapsar no cambió de estado visual.');
    }
    await toggleBtn.click();

    // search + highlight
    const searchInput = page.getByPlaceholder(/Buscar nodo/i);
    await searchInput.fill('invent');
    const marks = await page.locator('.menu-builder-crm-name mark').count();
    if (marks < 1) {
      await fail(page, 'busqueda-highlight', 'No se resaltan coincidencias al buscar.');
    }
    await searchInput.fill('');

    // edit prompt
    const invEditBtn = invRow.getByRole('button', { name: /Editar nombre del [ií]tem/i });
    const editDialogHandler = async (d) => {
      const msg = d.message();
      if (d.type() === 'prompt' && /Nombre/i.test(msg)) {
        await d.accept('Inventario QA');
        return;
      }
      if (d.type() === 'prompt' && /[ií]cono/i.test(msg)) {
        await d.accept('📦');
        return;
      }
      await d.accept();
    };
    page.on('dialog', editDialogHandler);
    await invEditBtn.click();
    await page.waitForTimeout(250);
    page.off('dialog', editDialogHandler);
    if ((await page.locator('.menu-builder-crm-name', { hasText: 'Inventario QA' }).count()) < 1) {
      await fail(page, 'edit-node', 'No se actualizó el nombre del nodo tras editar.');
    }

    // create + delete child (prompt + confirm)
    const invRowUpdated = page.locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: /Inventario QA/i }) }).first();
    const addFolderBtn = invRowUpdated.getByRole('button', { name: /Agregar carpeta hija/i });
    const expandBtn = invRowUpdated.locator('button[title="Expandir"]').first();
    if (await expandBtn.count()) {
      await expandBtn.click();
    }
    page.once('dialog', async (d) => {
      await d.accept('QA Folder Tmp');
    });
    await addFolderBtn.click();
    const tmpNode = page.locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: 'QA Folder Tmp' }) }).first();
    await tmpNode.waitFor({ state: 'attached', timeout: 4000 }).catch(() => {});
    if ((await tmpNode.count()) < 1) {
      await fail(page, 'add-subfolder', 'No se creó la subcarpeta temporal.');
    }
    const delBtn = tmpNode.getByRole('button', { name: /Eliminar ítem/i });
    page.once('dialog', async (d) => {
      const msg = d.message();
      if (!/Eliminar carpeta/i.test(msg)) {
        await d.dismiss();
        return;
      }
      await d.accept();
    });
    await delBtn.click();
    if (await tmpNode.isVisible()) {
      await fail(page, 'delete-node', 'El nodo temporal no se eliminó tras confirmar.');
    }

    // add subform
    const addFormBtn = invRowUpdated.getByRole('button', { name: /Agregar formulario hijo/i });
    page.once('dialog', async (d) => {
      await d.accept('QA Form Tmp');
    });
    await addFormBtn.click();
    const tmpFormNode = page.locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: 'QA Form Tmp' }) }).first();
    await tmpFormNode.waitFor({ state: 'attached', timeout: 4000 }).catch(() => {});
    if ((await tmpFormNode.count()) < 1) {
      await fail(page, 'add-subform', 'No se creó el subformulario temporal.');
    }

    // delete temporary form
    const delFormBtn = tmpFormNode.getByRole('button', { name: /Eliminar ítem/i });
    page.once('dialog', async (d) => {
      await d.accept();
    });
    await delFormBtn.click();
    if ((await tmpFormNode.count()) > 0) {
      await fail(page, 'delete-subform', 'El subformulario temporal no se eliminó.');
    }

    // restore edited name
    const invEditedRow = page.locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: /Inventario QA/i }) }).first();
    const restoreBtn = invEditedRow.getByRole('button', { name: /Editar nombre del [ií]tem/i });
    const restoreDialogHandler = async (d) => {
      if (d.type() === 'prompt' && /Nombre/i.test(d.message())) {
        await d.accept('Inventario');
        return;
      }
      if (d.type() === 'prompt' && /[ií]cono/i.test(d.message())) {
        await d.accept('📦');
        return;
      }
      await d.accept();
    };
    page.on('dialog', restoreDialogHandler);
    await restoreBtn.click();
    await page.waitForTimeout(250);
    page.off('dialog', restoreDialogHandler);

    // checkbox behavior by plan
    const invCheckbox = page
      .locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: /^Inventario$/i }) })
      .first()
      .locator('input[type="checkbox"]')
      .first();
    await page.getByRole('tab', { name: /STARTER/i }).click();
    await invCheckbox.setChecked(false);
    await page.getByRole('tab', { name: /BUSINESS/i }).click();
    await invCheckbox.setChecked(true);
    await page.getByRole('tab', { name: /STARTER/i }).click();
    if (await invCheckbox.isChecked()) {
      await fail(page, 'plan-checkbox-persistence', 'Los checkboxes no se aislan por plan (STARTER quedó marcado).');
    }
    await page.getByRole('tab', { name: /BUSINESS/i }).click();
    if (!(await invCheckbox.isChecked())) {
      await fail(page, 'plan-checkbox-persistence', 'Los checkboxes no persistieron al volver a BUSINESS.');
    }

    // new plan modal
    await page.getByRole('button', { name: /\+ Nuevo Plan/i }).click();
    const modal = page.locator('.menu-plan-composer__modalCard', { hasText: /Crear nuevo plan comercial/i });
    if (!(await modal.isVisible())) {
      await fail(page, 'new-plan-modal', 'No abrió modal de nuevo plan.');
    }
    const uniq = `QAPLAN${Date.now().toString().slice(-4)}`;
    await modal.getByLabel('Nombre del plan').fill(uniq);
    await modal.getByLabel('Precio mensual (USD)').fill('120');
    await modal.getByRole('button', { name: /Crear plan/i }).click();
    if (!(await page.getByRole('tab', { name: new RegExp(uniq, 'i') }).isVisible())) {
      await fail(page, 'new-plan-created', 'No apareció tab del nuevo plan creado.');
    }
    await page.getByRole('tab', { name: new RegExp(uniq, 'i') }).click();
    const checkedInNewPlan = await page.locator('.menu-builder-row--crm input[type="checkbox"]:checked').count();
    if (checkedInNewPlan > 0) {
      await fail(page, 'new-plan-empty', 'El nuevo plan no inicia vacío (tiene nodos activos).');
    }

    // layout persistence basic (toggle + switch plan + back)
    await page.getByLabel(/Vertical/i).check();
    await page.getByRole('tab', { name: /STARTER/i }).click();
    await page.getByRole('tab', { name: new RegExp(uniq, 'i') }).click();
    const verticalChecked = await page.getByLabel(/Vertical/i).isChecked();
    if (!verticalChecked) {
      await fail(page, 'layout-persistence', 'No se preservó layout por plan al cambiar de tab.');
    }

    // inheritance button
    await page.getByRole('tab', { name: /STARTER/i }).click();
    await invCheckbox.setChecked(true);
    await page.getByRole('tab', { name: /BUSINESS/i }).click();
    await page.getByRole('button', { name: /Copiar herencia/i }).click();
    if (!(await invCheckbox.isChecked())) {
      await fail(page, 'copy-inheritance', 'Copiar herencia no activó nodos heredados en BUSINESS.');
    }

    // reset plan button
    page.once('dialog', async (d) => {
      await d.accept();
    });
    await page.getByRole('button', { name: /Resetear plan/i }).click();
    const checkedAfterReset = await page.locator('.menu-builder-row--crm input[type="checkbox"]:checked').count();
    if (checkedAfterReset !== 0) {
      await fail(page, 'reset-plan', 'Resetear plan no limpió todas las activaciones.');
    }

    // price yearly toggle
    const priceBefore = await page.locator('.menu-plan-composer__planCardPrice').first().innerText();
    const annualToggle = page.getByLabel(/Mostrar precio anual estimado/i);
    if ((await annualToggle.count()) < 1) {
      await fail(page, 'price-toggle-control', 'No se encontró el toggle de precio anual.');
    }
    await annualToggle.check();
    const priceAfter = await page.locator('.menu-plan-composer__planCardPrice').first().innerText();
    if (priceBefore === priceAfter) {
      await fail(page, 'price-toggle', 'Mostrar anual no actualizó el precio de la tarjeta.');
    }

    // catalog preview button
    const previewBtn = page.getByRole('button', { name: /Previsualizar/i }).first();
    if (!(await previewBtn.isVisible())) {
      await fail(page, 'catalog-preview-button', 'No se encontró botón Previsualizar en catálogo.');
    }
    await previewBtn.click();
    const mockupDialog = page.getByRole('dialog', { name: /Mockup de formulario/i });
    if (!(await mockupDialog.isVisible().catch(() => false))) {
      await page.evaluate(() => {
        const btn = document.querySelector('.menu-builder-lib-previewRow button');
        if (btn instanceof HTMLButtonElement) btn.click();
      });
    }
    if (!(await mockupDialog.isVisible().catch(() => false))) {
      await fail(page, 'catalog-preview-modal', 'Previsualizar no mostró el mockup del formulario.');
    }
    await page.keyboard.press('Escape');

    // localStorage persistence (reload)
    await page.getByRole('tab', { name: /STARTER/i }).click();
    await invCheckbox.setChecked(true);
    await page.waitForFunction(() => {
      try {
        const raw = window.localStorage.getItem('crmPlanActiveNodeIds');
        if (!raw) return false;
        const parsed = JSON.parse(raw);
        const starter = parsed?.starter;
        return Array.isArray(starter) && starter.length > 0;
      } catch {
        return false;
      }
    });
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle');
    await openBuilderTab();
    await page.getByRole('tab', { name: /STARTER/i }).click();
    const invCheckboxReloaded = page
      .locator('.menu-builder-row--crm', { has: page.locator('.menu-builder-crm-name', { hasText: /^Inventario$/i }) })
      .first()
      .locator('input[type="checkbox"]')
      .first();
    if (!(await invCheckboxReloaded.isChecked())) {
      await fail(page, 'localstorage-persist', 'Las activaciones no persistieron tras recargar.');
    }

    // audit has records and clear works
    const auditEntries = page.locator('.menu-plan-composer__auditList li:not(.subtle)');
    if ((await auditEntries.count()) < 1) {
      await fail(page, 'audit-records', 'No se registraron acciones en auditoría.');
    }
    const clearAuditBtn = page.locator('.menu-plan-composer__auditActions button[aria-label*="Limpiar"]').first();
    if ((await clearAuditBtn.count()) < 1) {
      await fail(page, 'audit-clear-control', 'No se encontró botón Limpiar de auditoría.');
    }
    if (!(await clearAuditBtn.isEnabled())) {
      await fail(page, 'audit-clear-control', 'El botón Limpiar está deshabilitado pese a existir acciones en auditoría.');
    }
    await clearAuditBtn.click();
    const auditAfterClear = await page.locator('.menu-plan-composer__audit').first().innerText();
    if (!/Las acciones/i.test(auditAfterClear)) {
      await fail(page, 'audit-clear', 'El botón Limpiar no vació el log de auditoría.');
    }

    // external placeholder tabs
    const planesTab = page.getByRole('tab', { name: /Planes y Suscripciones/i });
    await planesTab.click();
    const auditTab = page.getByRole('tab', { name: /Auditoría Global/i });
    await auditTab.click();

    const passShot = path.join(artifactsDir, 'PASS-summary.png');
    await page.screenshot({ path: passShot, fullPage: true });
    console.log(JSON.stringify({ status: 'pass', screenshot: passShot }, null, 2));
  } finally {
    await context.close();
    await browser.close();
  }
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});
