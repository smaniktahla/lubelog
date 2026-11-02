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
        Task<OperationResponse> GetMakes(bool motorcycle);
        Task<OperationResponse> GetModels(string make, int year, bool motorcycle);
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
                var json = await GetVpicJson($"DecodeVinValues/{vin}?format=json");
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
        public async Task<OperationResponse> GetMakes(bool motorcycle)
        {
            var type = motorcycle ? "motorcycle" : "car";
            var cacheKey = $"makes_{type}";
            if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
            {
                return OperationResponse.Succeed(string.Empty, cached);
            }
            try
            {
                var json = await GetVpicJson($"GetMakesForVehicleType/{type}?format=json");
                var makes = ParseNames(json, "MakeName").Select(NormalizeMake).Distinct().OrderBy(x => x).ToList();
                _cache.Set(cacheKey, makes, CacheDuration);
                return OperationResponse.Succeed(string.Empty, makes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Make lookup failed");
                return OperationResponse.Failed("Make lookup is unavailable");
            }
        }
        public async Task<OperationResponse> GetModels(string make, int year, bool motorcycle)
        {
            make = (make ?? string.Empty).Trim();
            if (make == string.Empty || year < 1900)
            {
                return OperationResponse.Failed("A make and a year after 1900 are required");
            }
            var type = motorcycle ? "/vehicletype/motorcycle" : string.Empty;
            var cacheKey = $"models_{make.ToLowerInvariant()}_{year}_{type}";
            if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
            {
                return OperationResponse.Succeed(string.Empty, cached);
            }
            try
            {
                var json = await GetVpicJson($"GetModelsForMakeYear/make/{Uri.EscapeDataString(make)}/modelyear/{year}{type}?format=json");
                var models = ParseNames(json, "Model_Name").Distinct().OrderBy(x => x).ToList();
                _cache.Set(cacheKey, models, CacheDuration);
                return OperationResponse.Succeed(string.Empty, models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model lookup failed");
                return OperationResponse.Failed("Model lookup is unavailable");
            }
        }
        private async Task<string> GetVpicJson(string path)
        {
            var baseUrl = (_config["VPIC_BASE_URL"] ?? DefaultBaseUrl).TrimEnd('/');
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            return await client.GetStringAsync($"{baseUrl}/{path}");
        }
        public static List<string> ParseNames(string json, string property)
        {
            using var doc = JsonDocument.Parse(json);
            var names = new List<string>();
            if (!doc.RootElement.TryGetProperty("Results", out var results))
            {
                return names;
            }
            foreach (var row in results.EnumerateArray())
            {
                if (row.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                {
                    names.Add(v.GetString()!.Trim());
                }
            }
            return names;
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
