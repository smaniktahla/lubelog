using CarCareTracker.Logic;
using CarCareTracker.Models;
using Xunit;

namespace CarCareTracker.Tests
{
    public class SpecHistoryLogicTests
    {
        private static ExtraField Spec(string name, string value = "") => new ExtraField { Name = name, Value = value };
        private static GenericRecord Rec(string date, int miles, string description, params string[] tags) =>
            new GenericRecord { Date = DateTime.Parse(date), Mileage = miles, Description = description, Tags = tags.ToList() };

        [Fact]
        public void OilChange_UpdatesOilSpecAndOilFilter_UsingLatestRecord()
        {
            var records = new[] { Rec("2025-01-10", 10000, "Oil change"), Rec("2026-03-04", 15000, "Oil Change + inspection") };
            var items = SpecHistoryLogic.Build(new[] { Spec("Oil Spec", "10W-40"), Spec("Oil Filter") }, records, 17000);
            var oil = items.Single(x => x.Name == "Oil Spec");
            Assert.Equal(DateTime.Parse("2026-03-04"), oil.LastDate);
            Assert.Equal(15000, oil.LastMileage);
            Assert.Equal(2000, oil.DistanceSince);
            Assert.True(items.Single(x => x.Name == "Oil Filter").HasHistory);
        }
        [Fact]
        public void AirFilter_DoesNotMatchCabinFilter_AndViceVersa()
        {
            var records = new[] { Rec("2026-01-01", 5000, "Replaced cabin air filter") };
            var items = SpecHistoryLogic.Build(new[] { Spec("Air Filter", "K&N"), Spec("Cabin Air Filter") }, records, 6000);
            Assert.False(items.Single(x => x.Name == "Air Filter").HasHistory);
            Assert.True(items.Single(x => x.Name == "Cabin Air Filter").HasHistory);
        }
        [Fact]
        public void ForkOil_IsNotAnEngineOilChange()
        {
            var records = new[] { Rec("2026-01-01", 5000, "Fork oil service") };
            var items = SpecHistoryLogic.Build(new[] { Spec("Oil Spec", "10W-40"), Spec("Fork Oil") }, records, 6000);
            Assert.False(items.Single(x => x.Name == "Oil Spec").HasHistory);
            Assert.True(items.Single(x => x.Name == "Fork Oil").HasHistory);
        }
        [Fact]
        public void Tag_MatchesEvenWhenDescriptionDoesNot()
        {
            var records = new[] { Rec("2026-02-02", 8000, "Track day prep", "Coolant") };
            var items = SpecHistoryLogic.Build(new[] { Spec("Coolant") }, records, 8500);
            Assert.True(items.Single().HasHistory);
        }
        [Fact]
        public void FrontAndRearTires_AreTrackedSeparately()
        {
            var records = new[] { Rec("2026-05-01", 9000, "New rear tire") };
            var items = SpecHistoryLogic.Build(new[] { Spec("Tire Size Front", "120/70ZR17"), Spec("Tire Size Rear", "190/55ZR17") }, records, 9500);
            Assert.False(items.Single(x => x.Name == "Tire Size Front").HasHistory);
            Assert.True(items.Single(x => x.Name == "Tire Size Rear").HasHistory);
        }
        [Fact]
        public void EmptySpecWithoutHistory_IsHidden_ValueOnlyIsKept()
        {
            var items = SpecHistoryLogic.Build(new[] { Spec("Chain"), Spec("Oil Capacity", "3.7 L") }, new List<GenericRecord>(), 100);
            Assert.Single(items);
            Assert.Equal("Oil Capacity", items[0].Name);
            Assert.False(items[0].HasHistory);
        }
        [Fact]
        public void DistanceSince_IsZeroWhenMileageUnknownOrOdometerBehind()
        {
            var items = SpecHistoryLogic.Build(new[] { Spec("Coolant") }, new[] { Rec("2026-01-01", 0, "Coolant flush") }, 5000);
            Assert.Equal(0, items.Single().DistanceSince);
            items = SpecHistoryLogic.Build(new[] { Spec("Coolant") }, new[] { Rec("2026-01-01", 6000, "Coolant flush") }, 5000);
            Assert.Equal(0, items.Single().DistanceSince);
        }
    }
}
