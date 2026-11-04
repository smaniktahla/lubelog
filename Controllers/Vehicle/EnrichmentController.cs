using CarCareTracker.Filter;
using CarCareTracker.Helper;
using CarCareTracker.Logic;
using CarCareTracker.Models;
using Microsoft.AspNetCore.Mvc;

namespace CarCareTracker.Controllers
{
    public partial class VehicleController
    {
        [HttpGet]
        public async Task<IActionResult> DecodeVin(string vin)
        {
            //resolved here instead of the constructor to keep the fork's diff to upstream files small.
            var enrichmentLogic = HttpContext.RequestServices.GetRequiredService<IVehicleEnrichmentLogic>();
            var result = await enrichmentLogic.DecodeVin(vin);
            return Json(result);
        }
        [TypeFilter(typeof(CollaboratorFilter))]
        [HttpGet]
        public IActionResult GetVehicleSpecSummary(int vehicleId)
        {
            var vehicle = _dataAccess.GetVehicleById(vehicleId);
            var records = _vehicleLogic.GetVehicleRecords(vehicleId);
            var currentMileage = _odometerLogic.GetLastOdometerRecordMileage(vehicleId, records.OdometerRecords);
            var history = records.ServiceRecords.Cast<GenericRecord>()
                .Concat(records.CollisionRecords)
                .Concat(records.UpgradeRecords);
            var items = SpecHistoryLogic.Build(VehicleSpecPresets.Merge(vehicle.Specs, vehicle.IsMotorcycle), history, currentMileage);
            return PartialView("_VehicleSpecSummary", items);
        }
        [HttpGet]
        public IActionResult GetSpecPreset(bool motorcycle = false)
        {
            return Json(VehicleSpecPresets.GetFieldNames(motorcycle));
        }
        [HttpGet]
        public async Task<IActionResult> GetMakes(bool motorcycle = false)
        {
            var enrichmentLogic = HttpContext.RequestServices.GetRequiredService<IVehicleEnrichmentLogic>();
            return Json(await enrichmentLogic.GetMakes(motorcycle));
        }
        [HttpGet]
        public async Task<IActionResult> GetModels(string make, int year, bool motorcycle = false)
        {
            var enrichmentLogic = HttpContext.RequestServices.GetRequiredService<IVehicleEnrichmentLogic>();
            return Json(await enrichmentLogic.GetModels(make, year, motorcycle));
        }
    }
}
