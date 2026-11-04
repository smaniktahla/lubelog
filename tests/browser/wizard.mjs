// Headless browser check of the add-vehicle wizard. Run against a disposable copy of the data, never production.
//   BASE_URL=http://host:7081 TEST_VIN=<17 chars> node wizard.mjs
// Creates a vehicle with plate BRWTEST and deletes it again at the end.
import { chromium } from 'playwright';

const base = process.env.BASE_URL;
const vin = process.env.TEST_VIN;
if (!base || !vin) { console.error('BASE_URL and TEST_VIN are required'); process.exit(2); }

let failures = 0;
const check = (name, ok, detail = '') => { console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  ' + detail : ''}`); if (!ok) failures++; };
const specLabels = page => page.$$eval('#vehicleSpecs .spec-field label', els => els.map(e => e.textContent));

const browser = await chromium.launch();
const page = await browser.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
await page.goto(`${base}/Home/Index?tab=garage`);

// car modal: preset without bike items
await page.evaluate(() => showAddVehicleModal());
await page.waitForSelector('#vehicleSpecs .spec-field', { state: 'attached' });
let labels = await specLabels(page);
check('car preset has Tire Size, no Chain', labels.includes('Tire Size') && !labels.includes('Chain'), `${labels.length} specs`);
check('VIN input and Lookup button render', await page.locator('#inputVin').count() === 1 && await page.locator('button:has-text("Lookup")').count() === 1);

// invalid VIN
await page.fill('#inputVin', 'BAD');
await page.click('button:has-text("Lookup")');
check('bad VIN shows an error and no review', (await page.locator('#vinLookupMessage').innerText()).includes('17 character') && await page.locator('#vinLookupReview').isHidden());

// valid VIN -> review -> apply
await page.fill('#inputVin', vin.toLowerCase());
check('VIN is upper-cased as typed', await page.inputValue('#inputVin') === vin.toUpperCase());
await page.click('button:has-text("Lookup")');
await page.waitForSelector('#vinLookupReview table');
const review = await page.locator('#vinLookupReview').innerText();
check('review lists decoded make/model/year', /BMW|Honda|Harley/i.test(review) && /Year/.test(review), review.replace(/\s+/g, ' ').slice(0, 120));
await page.click('#vinLookupReview button:has-text("Apply")');
await page.waitForFunction(() => document.querySelector('#vinLookupReview').classList.contains('d-none'));
const filled = { year: await page.inputValue('#inputYear'), make: await page.inputValue('#inputMake'), model: await page.inputValue('#inputModel') };
check('Apply filled year/make/model', !!filled.year && !!filled.make && !!filled.model, JSON.stringify(filled));
const moto = await page.isChecked('#inputLookupMotorcycle');
labels = await specLabels(page);
check('motorcycle VIN switches to the motorcycle preset', moto === labels.includes('Chain'), `moto=${moto}, ${labels.length} specs`);
check('Engine spec filled from decode', (await page.inputValue('#vehicleSpecs input[placeholder="Engine"]')).length > 0);
await page.waitForFunction(() => document.querySelectorAll('#vehicleMakeList option').length > 100 && document.querySelectorAll('#vehicleModelList option').length > 0, null, { timeout: 15000 }).catch(() => {});
check('make/model suggestions loaded', await page.locator('#vehicleMakeList option').count() > 100 && await page.locator('#vehicleModelList option').count() > 0);

// save, reopen, verify
await page.fill('#vehicleSpecs input[placeholder="Oil Filter"]', 'BRW-FILTER');
await page.fill('#inputLicensePlate', 'BRWTEST');
await page.evaluate(() => saveVehicle(false));
await page.waitForFunction(() => document.body.innerText.includes('BRWTEST'));
const vehicles = await (await page.request.get(`${base}/api/vehicles`)).json();
const saved = vehicles.find(v => v.licensePlate === 'BRWTEST');
check('vehicle saved', !!saved);
if (saved) {
  await page.evaluate(id => $.get(`/Vehicle/GetEditVehiclePartialViewById?vehicleId=${id}`, d => $('#addVehicleModalContent').html(d)), saved.id);
  await page.waitForSelector('#inputVin');
  check('VIN persisted', await page.inputValue('#inputVin') === vin.toUpperCase());
  check('spec persisted', await page.inputValue('#vehicleSpecs input[placeholder="Oil Filter"]') === 'BRW-FILTER');
  check('motorcycle flag persisted', await page.isChecked('#inputLookupMotorcycle') === moto);
  // last-changed summary: add an oil change through the API, then read the dashboard summary
  const add = await page.request.post(`${base}/api/vehicle/servicerecords/add?vehicleId=${saved.id}`, {
    data: { date: '2026-03-04', odometer: '15000', description: 'Oil change', notes: '', cost: '50' }
  });
  check('service record added via API', add.ok(), String(add.status()));
  await page.request.post(`${base}/api/vehicle/odometerrecords/add?vehicleId=${saved.id}`, { data: { date: '2026-05-01', odometer: '17000', notes: '' } });
  const summary = await (await page.request.get(`${base}/Vehicle/GetVehicleSpecSummary?vehicleId=${saved.id}`)).text();
  const row = summary.replace(/\s+/g, ' ').match(/<tr> <td>Oil Filter<\/td>.*?<\/tr>/)?.[0] ?? '';
  check('summary shows Oil Filter last changed with odometer', row.includes('BRW-FILTER') && row.includes('15,000'), row.replace(/<[^>]+>/g, ' ').trim());
  check('summary shows distance since change', row.includes('2,000'));
  check('Engine spec listed without history', /<td>Engine<\/td>/.test(summary));
  // dashboard actually renders the summary
  await page.goto(`${base}/Vehicle/Index?vehicleId=${saved.id}&tab=report`);
  await page.waitForSelector('#vehicleSpecSummary table');
  check('dashboard shows the Maintenance Specs table', (await page.locator('#vehicleSpecSummary').innerText()).includes('Oil Filter'));
  await page.request.post(`${base}/Vehicle/DeleteVehicle?vehicleId=${saved.id}`);
  const after = await (await page.request.get(`${base}/api/vehicles`)).json();
  check('test vehicle cleaned up', !after.some(v => v.licensePlate === 'BRWTEST'));
}
check('no page errors', errors.length === 0, errors.join('; '));
await browser.close();
console.log(failures ? `${failures} FAILED` : 'ALL PASSED');
process.exit(failures ? 1 : 0);
