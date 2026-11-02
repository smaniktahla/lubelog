namespace CarCareTracker.Models
{
    /// <summary>
    /// Normalized result of decoding a VIN through NHTSA vPIC.
    /// </summary>
    public class VinDecodeResult
    {
        public string Vin { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Trim { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string BodyClass { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public bool IsMotorcycle { get; set; }
        /// <summary>
        /// One of Gasoline, Diesel, Electric to match Vehicle fuel type options.
        /// </summary>
        public string FuelType { get; set; } = "Gasoline";
        public decimal DisplacementCC { get; set; }
        public int Cylinders { get; set; }
        public decimal HorsePower { get; set; }
        public string Cooling { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string PlantCountry { get; set; } = string.Empty;
        /// <summary>
        /// False for many non-North American VINs, which are not required to carry a valid check digit.
        /// </summary>
        public bool CheckDigitValid { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
