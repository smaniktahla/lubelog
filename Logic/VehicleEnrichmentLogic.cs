using CarCareTracker.Helper;
using CarCareTracker.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text.Json;

namespace CarCareTracker.Logic
{
    public interface IVehicleEnrichmentLogic
    {
        Task<OperationResponse> DecodeVin(string vin);
    }
    /// <summary>
    /// Decodes VINs through the free NHTSA vPIC API. Override the endpoint with VPIC_BASE_URL.
    /// </summary>
    public class VehicleEnrichmentLogic : IVehicleEnrichmentLogic
    {
        private const string DefaultBaseUrl = "https://vpic.nhtsa.dot.gov/api/vehicles";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<IVehicleEnrichmentLogic> _logger;
        public VehicleEnrichmentLogic(IHttpClientFactory httpClientFactory, IMemoryCache cache, IConfiguration config, ILogger<IVehicleEnrichmentLogic> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _config = config;
            _logger = logger;
        }
        public async Task<OperationResponse> DecodeVin(string vin)
        {
            vin = VinHelper.Normalize(vin);
            if (!VinHelper.IsValidFormat(vin))
            {
                return OperationResponse.Failed("VIN must be 17 characters and cannot contain I, O or Q");
            }
            if (_cache.TryGetValue($"vin_{vin}", out VinDecodeResult? cached) && cached != null)
            {
                return OperationResponse.Succeed("VIN decoded", cached);
            }
            try
            {
                var baseUrl = (_config["VPIC_BASE_URL"] ?? DefaultBaseUrl).TrimEnd('/');
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var json = await client.GetStringAsync($"{baseUrl}/DecodeVinValues/{vin}?format=json");
                var result = ParseVpicResponse(vin, json);
                if (result == null)
                {
                    return OperationResponse.Failed("VIN could not be decoded");
                }
                _cache.Set($"vin_{vin}", result, CacheDuration);
                return OperationResponse.Succeed("VIN decoded", result);
            }
            catch (Exception ex)
            {
                //lookups are optional, callers fall back to manual entry.
                _logger.LogError(ex, "VIN lookup failed");
                return OperationResponse.Failed("VIN lookup is unavailable, enter the details manually");
            }
        }
        /// <summary>
        /// Maps a vPIC DecodeVinValues response. Returns null when nothing usable was decoded.
        /// </summary>
        public static VinDecodeResult? ParseVpicResponse(string vin, string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Results", out var results) || results.GetArrayLength() == 0)
            {
                return null;
            }
            var row = results[0];
            string Get(string name) => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty).Trim() : string.Empty;
            var make = Get("Make");
            var model = Get("Model");
            if (make == string.Empty && model == string.Empty)
            {
                return null;
            }
            var vehicleType = Get("VehicleType");
            var fuel = Get("FuelTypePrimary");
            var result = new VinDecodeResult
            {
                Vin = vin,
                Make = NormalizeMake(make),
                Model = model,
                Trim = Get("Trim"),
                Series = Get("Series"),
                BodyClass = Get("BodyClass"),
                VehicleType = vehicleType,
                IsMotorcycle = vehicleType.Equals("MOTORCYCLE", StringComparison.OrdinalIgnoreCase),
                FuelType = fuel.Contains("Diesel", StringComparison.OrdinalIgnoreCase) ? "Diesel" : fuel.Contains("Electric", StringComparison.OrdinalIgnoreCase) ? "Electric" : "Gasoline",
                Cooling = Get("CoolingType"),
                DriveType = Get("DriveType"),
                Manufacturer = Get("Manufacturer"),
                PlantCountry = Get("PlantCountry"),
                CheckDigitValid = VinHelper.HasValidCheckDigit(vin)
            };
            if (int.TryParse(Get("ModelYear"), out var year))
            {
                result.Year = year;
            }
            if (int.TryParse(Get("EngineCylinders"), out var cylinders))
            {
                result.Cylinders = cylinders;
            }
            if (decimal.TryParse(Get("DisplacementCC"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cc))
            {
                result.DisplacementCC = Math.Round(cc);
            }
            if (decimal.TryParse(Get("EngineHP"), NumberStyles.Any, CultureInfo.InvariantCulture, out var hp))
            {
                result.HorsePower = hp;
            }
            var speeds = Get("TransmissionSpeeds");
            var style = Get("TransmissionStyle");
            result.Transmission = string.Join(" ", new[] { speeds != string.Empty ? $"{speeds}-speed" : string.Empty, style }.Where(x => x != string.Empty));
            if (!result.CheckDigitValid)
            {
                result.Warnings.Add("VIN check digit does not match. This is normal for some non-North American VINs, but double check the VIN.");
            }
            var additionalError = Get("AdditionalErrorText");
            if (additionalError != string.Empty)
            {
                result.Warnings.Add(additionalError);
            }
            return result;
        }
        /// <summary>
        /// vPIC returns upper case makes. Keep short acronyms (BMW, GMC), title case the rest.
        /// </summary>
        public static string NormalizeMake(string make)
        {
            if (make.Length <= 3 || make != make.ToUpperInvariant())
            {
                return make;
            }
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(make.ToLowerInvariant());
        }
    }
}
