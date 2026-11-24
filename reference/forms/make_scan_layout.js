// Measures the answer boxes on the forms clients fill in by hand and writes them to
// src/FuelIntake.Core/Import/scan-forms.json (inches from each page's top-left corner).
// Run after changing IntakePage1Form, IntakePage2Form or IncomeExpenseForm (and bump ScanForms.Version if boxes moved):
//   dotnet run --project src/FuelIntake.DevHost -p:OfflinePreview=true   (in another terminal)
//   node reference/forms/make_scan_layout.js [http://localhost:5000]
// Chromium is the same engine WebView2 prints with, so the boxes land where the app prints them.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const base = process.argv[2] || 'http://localhost:5000';
const forms = ['IntakePage1', 'IntakePage2', 'IncomeExpense'];
const version = 1; // ScanForms.Version
const codes = { IntakePage1: 1, IntakePage2: 2, IncomeExpense: 3 }; // ScanForms.Codes

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.emulateMedia({ media: 'print' });
  await page.goto(`${base}/print-html?blank=true&forms=${forms.join(',')}`);
  const sheets = await page.evaluate(() => [...document.querySelectorAll('.sheet')].map(sheet => {
    const code = sheet.querySelector('[data-scan-form]');
    if (!code) return null;
    const s = sheet.getBoundingClientRect();
    const r = v => Math.round(v / 96 * 1000) / 1000; // CSS px → inches
    return {
      id: code.dataset.scanForm,
      pageWidth: r(s.width), pageHeight: r(s.height),
      zones: [...sheet.querySelectorAll('[data-scan]')].map(e => {
        const b = e.getBoundingClientRect();
        const kind = e.tagName === 'TD' ? 'Cell' : e.dataset.kind === 'tick' ? 'Tick' : 'Text';
        const z = { path: e.dataset.scan, kind, x: r(b.left - s.left), y: r(b.top - s.top), w: r(b.width), h: r(b.height) };
        // an empty "___" tick blank is just its underline: the box is the writing height above that line
        if (kind === 'Tick' && z.h < 0.1) { z.y = r(b.bottom - s.top - 16); z.h = r(16); }
        if (e.dataset.value) z.value = e.dataset.value;
        return z;
      }),
    };
  }).filter(Boolean));
  await browser.close();
  const out = sheets.map(s => ({ id: s.id, code: codes[s.id], version, pageWidth: s.pageWidth, pageHeight: s.pageHeight, zones: s.zones }));
  const file = path.join(__dirname, '..', '..', 'src', 'FuelIntake.Core', 'Import', 'scan-forms.json');
  fs.writeFileSync(file, JSON.stringify(out, null, 1) + '\n');
  for (const s of out) console.log(`${s.id}: ${s.zones.length} boxes, ${s.pageWidth}×${s.pageHeight} in`);
})();
