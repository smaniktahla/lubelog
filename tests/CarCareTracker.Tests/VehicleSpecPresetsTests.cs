using CarCareTracker.Helper;
using CarCareTracker.Models;
using Xunit;

namespace CarCareTracker.Tests
{
    public class VehicleSpecPresetsTests
    {
        [Fact]
        public void Motorcycle_HasRequestedItems()
        {
            var names = VehicleSpecPresets.GetFieldNames(true);
            foreach (var expected in new[] { "Chain", "Front Sprocket", "Rear Sprocket", "Valve Clearance", "Fork Oil", "Coolant", "Air Filter", "Oil Filter", "Spark Plugs", "Battery Form Factor", "Battery Capacity", "Tire Size Front", "Tire Size Rear" })
            {
                Assert.Contains(expected, names);
            }
        }
        [Fact]
        public void Car_HasNoChainOrSprockets()
        {
            var names = VehicleSpecPresets.GetFieldNames(false);
            Assert.DoesNotContain("Chain", names);
            Assert.Contains("Tire Size", names);
            Assert.Contains("Oil Filter", names);
        }
        [Fact]
        public void Merge_KeepsSavedValuesAndExtras()
        {
            var saved = new List<ExtraField> {
                new ExtraField { Name = "Oil Filter", Value = "HF160" },
                new ExtraField { Name = "Custom Note", Value = "keep me" },
                new ExtraField { Name = "Empty Custom", Value = "" }
            };
            var merged = VehicleSpecPresets.Merge(saved, true);
            Assert.Equal("HF160", merged.First(x => x.Name == "Oil Filter").Value);
            Assert.Equal("keep me", merged.First(x => x.Name == "Custom Note").Value);
            Assert.DoesNotContain(merged, x => x.Name == "Empty Custom");
            Assert.Equal(merged.Count, merged.Select(x => x.Name).Distinct().Count());
        }
        [Fact]
        public void Merge_NullSaved_ReturnsBlankPreset()
        {
            var merged = VehicleSpecPresets.Merge(null, false);
            Assert.Equal(VehicleSpecPresets.GetFieldNames(false).Count, merged.Count);
            Assert.All(merged, x => Assert.Equal(string.Empty, x.Value));
        }
    }
}
