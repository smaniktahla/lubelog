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
    }
}
