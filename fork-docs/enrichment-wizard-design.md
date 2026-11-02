# Vehicle Enrichment Wizard — Design

Fork of hargata/lubelog, branched from v1.7.4. Goal: entering a new vehicle (VIN, or
year/make/model) auto-populates basic data and maintenance specs, and the garage shows
"last changed / serviced" per item. Motorcycles are first-class.

## What the code already gives us (v1.7.4)

- `Models/Vehicle/Vehicle.cs`: Year, Make, Model, LicensePlate, IsElectric/IsDiesel, UseHours,
  **`ExtraFields` (name/value/type/required)** and **`Tags`**. No VIN, no trim, no engine.
- Admin-defined extra fields per record type (`RecordExtraField`, `IExtraFieldDataAccess`;
  `ImportMode.VehicleRecord` = vehicle-level fields). They already show in the add/edit vehicle modal.
- Add flow: `garage.js showAddVehicleModal` -> `VehicleController.AddVehiclePartialView`
  -> `Views/Vehicle/_VehicleModal.cshtml` -> `shared.js saveVehicle()` -> `VehicleController.SaveVehicle`.
- REST API already has `POST /api/vehicles/add` and `/api/vehicles/update` (`APIController.cs`).
- Per-vehicle record search exists (`VehicleController.SearchRecords`, `SearchRecordsByTags`);
  nothing searches *across* vehicles by attribute.
- Equipment, Inspection and Collision record types already exist, plus Supplies and Reminders.
- Data access has LiteDB and Postgres implementations (`External/Implementations`); any model change must work in both.
- Build: Dockerfile uses the .NET 10 SDK image. No `dotnet` on this host, so build and test via Docker.

## Design decisions

0. **VIN is a real field: `Vehicle.Vin`** (decided). One additive property plus one modal input. Validated
   (17 chars, check digit, upper-case, no I/O/Q), always present, anchors decode/re-decode and duplicate detection.
   Verify Postgres storage compatibility in PR 1.
1. **Store specs in `Vehicle.ExtraFields`, not a new model.** Zero schema change for LiteDB and Postgres,
   already in the API, import/export, edit modal and backups. The wizard writes fields with reserved names:
   `Trim`, `Engine`, `Oil Spec`, `Oil Capacity`, `Oil Filter`, `Air Filter`, `Tire Size (Front)`,
   `Tire Size (Rear)`, `Coolant`, `Brake Fluid`. Cost: values are strings. If we later need typed or
   queryable specs, migrate to a `Specs` dictionary on `Vehicle` (decide after phase 2).
2. **Provenance per field.** Alongside each enriched value keep a source marker (`decoded` = NHTSA,
   `drafted` = LLM, `manual`). Stored as a single extra field `Spec Sources` (JSON string) so no schema change,
   shown as small badges in the review step. Drafted values must be confirmed before save.
3. **Server-side enrichment service** (`Logic/VehicleEnrichmentLogic.cs`, interface in `External/Interfaces`,
   registered in `Program.cs`). The browser never calls NHTSA directly (CORS, caching, offline fallback).
4. **Cache** NHTSA responses (by VIN and by year/make/model) in the existing data folder (small LiteDB
   collection or JSON file) with long TTL; vPIC data barely changes.
5. **Additive diff.** New files wherever possible; edits to upstream files limited to: `_VehicleModal.cshtml`
   (a "Lookup" row), `garage.js`/`shared.js` (wizard hooks), `Program.cs` (DI), `VehicleController` (new actions).
   This keeps rebases on upstream releases cheap.

## Components

### 1. NHTSA vPIC client
- `GET https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{VIN}?format=json` -> make, model, year, trim,
  body class, engine (displacement, cylinders, HP), fuel type, drive type, vehicle type
  (motorcycle detection), plant.
- `GetAllMakes`, `GetModelsForMakeYear/make/{make}/modelyear/{year}` for the year/make/model path
  (typeahead; also covers pre-1981 vehicles with no VIN decode).
- Maps to `Vehicle`: Year/Make/Model, `IsDiesel`/`IsElectric` from fuel type, plus extra fields for the rest.
- Failure mode: timeout/5xx -> wizard falls back to manual entry with a notice. Never blocks saving.

### 2. Maintenance spec drafting (LLM, optional)
- Input: decoded vehicle + optional owner's-manual **PDF upload and/or pasted text** (decided: both). PDF text is
  extracted server-side; a PDF with no text layer (scanned) is flagged, no OCR in v1. Output: strict JSON of the spec fields above.
- Runs on a local model (AI2, OpenAI-compatible endpoint configured via env: `ENRICH_LLM_URL`, `ENRICH_LLM_MODEL`;
  feature is off when unset, so upstream-style deployments are unaffected).
- Every drafted value is marked `drafted` and requires an explicit confirm in the review step. Prompt must say
  "unknown" is acceptable; never invent part numbers.

### 3. Wizard UI
Replaces nothing; adds a "Quick add" path in the add-vehicle modal:
1. **Identify:** VIN box (or Year/Make/Model typeahead, or "Motorcycle" toggle) -> Lookup.
2. **Review:** table of proposed values with source badges, editable inline; drafted rows highlighted.
3. **Save:** fills the existing modal fields and extra fields, then the normal `saveVehicle()` runs.
   Motorcycle preset sets `UseHours` off and adds: Chain, Front/Rear Sprocket, Valve Clearance, Fork Oil,
   Coolant, Air Filter, Oil Filter, Spark Plugs, Battery (form factor + capacity), Tire Size Front/Rear,
   and seeds default reminders (oil, chain, tires).

### 4. "Last changed / serviced"
- A spec-to-record mapping: each item (Oil, Oil Filter, Air Filter, Tires, Brake Fluid, Coolant, Chain)
  maps to service-record **tags** (and description keywords as fallback).
- Computed at view time from existing service/repair/upgrade records: latest matching record gives
  date + odometer; no stored duplicate (compute-then-store only if performance needs it).
- Shown on the vehicle page next to each spec, and on the garage card for oil.

### 5. Cross-vehicle spec search
- New action scanning `Vehicle.ExtraFields` across accessible vehicles ("which vehicles use 225/45R17?").
  Respects household access via `UserLogic`.

## Phases / PR breakdown

| PR | Scope | Upstreamable |
|----|-------|--------------|
| 1 | vPIC client + cache + `VehicleEnrichmentLogic` + unit tests (mock HTTP) | Yes |
| 2 | Quick-add UI: VIN/YMM lookup, review step, fills modal + extra fields, provenance | Yes (generic part) |
| 3 | Spec field set + motorcycle preset + default reminders | Maybe |
| 4 | "Last changed" computation + display | Yes |
| 5 | Cross-vehicle spec search | Yes |
| 6 | LLM spec drafting (env-gated) | No (fork-only) |

## Deploy / safety
- Build image from the fork (`ghcr.io/smaniktahla/lubelog` or local tag) and run it on a **separate port
  against a copy of the data volume** before switching ubuntu-box (:7080). Backups are in
  `/home/salil/backups/`.
- Public repo: no API keys or personal vehicle data committed; endpoints via environment variables.

## Decisions
- **Specs live in `Vehicle.Specs` (revised in PR 3), not `ExtraFields`.** LubeLogger prunes a vehicle's extra
  fields to the admin's global definitions on edit (`StaticHelper.AddExtraFields`), so per-vehicle-type fields
  (chain, sprockets on a bike; none on a car) cannot be stored there. `Vehicle.Specs` is a `List<ExtraField>`
  with no global template; `Vehicle.IsMotorcycle` selects the preset (`Helper/VehicleSpecPresets.cs`).
- VIN is a first-class `Vehicle.Vin` field.
- Manual input accepts PDF upload and pasted text.
- Motorcycle item list: chain, sprockets, valve clearance, fork oil, coolant, air filter, oil filter,
  spark plugs, battery form factor + capacity (plus tires).

## Open questions
1. Keep Postgres compatibility for upstream PRs (production uses LiteDB)?
2. OCR for scanned manuals: later phase?
3. Test fixtures: use sanitized vPIC responses (dummy VIN) for a car and a motorcycle; never commit real VINs.
