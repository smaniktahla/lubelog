using CarCareTracker.Models;

namespace CarCareTracker.Helper
{
    /// <summary>
    /// Maintenance spec fields offered for new vehicles. Saved values live on Vehicle.Specs, which (unlike Vehicle.ExtraFields)
    /// is not tied to the admin's global extra field definitions, so a motorcycle and a car can carry different fields.
    /// </summary>
    public static class VehicleSpecPresets
    {
        private static readonly string[] Car = {
            "Trim", "Engine", "Transmission", "Drive Type",
            "Oil Spec", "Oil Capacity", "Oil Filter", "Air Filter", "Cabin Air Filter", "Spark Plugs",
            "Battery Form Factor", "Battery Capacity", "Tire Size", "Coolant", "Brake Fluid", "Transmission Fluid"
        };
        private static readonly string[] Motorcycle = {
            "Trim", "Engine", "Transmission",
            "Oil Spec", "Oil Capacity", "Oil Filter", "Air Filter", "Spark Plugs",
            "Battery Form Factor", "Battery Capacity", "Tire Size Front", "Tire Size Rear",
            "Chain", "Front Sprocket", "Rear Sprocket", "Valve Clearance", "Fork Oil", "Coolant"
        };
        public static List<string> GetFieldNames(bool motorcycle)
        {
            return (motorcycle ? Motorcycle : Car).ToList();
        }
        /// <summary>
        /// Preset fields first (keeping saved values), then any saved fields that are not in the preset.
        /// </summary>
        public static List<ExtraField> Merge(List<ExtraField>? saved, bool motorcycle)
        {
            saved ??= new List<ExtraField>();
            var merged = GetFieldNames(motorcycle)
                .Select(name => new ExtraField { Name = name, Value = saved.FirstOrDefault(x => x.Name == name)?.Value ?? string.Empty })
                .ToList();
            merged.AddRange(saved.Where(x => !string.IsNullOrWhiteSpace(x.Value) && !merged.Any(m => m.Name == x.Name)));
            return merged;
        }
    }
}
