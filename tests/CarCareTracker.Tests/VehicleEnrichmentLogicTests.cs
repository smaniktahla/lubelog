using CarCareTracker.Logic;
using CarCareTracker.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace CarCareTracker.Tests
{
    public class VehicleEnrichmentLogicTests
    {
        //dummy VINs, fixtures are sanitized. Never commit real VINs.
        private const string MotorcycleVin = "WB10E8300R6J00000";
        private const string CarVin = "1HGCR2F3XGA000000";

        private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

        private class StubHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }
            public Func<HttpResponseMessage> Respond { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(Respond());
            }
        }
        private class StubFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;
            public StubFactory(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false);
        }
        private static VehicleEnrichmentLogic Build(StubHandler handler)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VPIC_BASE_URL"] = "http://vpic.test/api/vehicles" }).Build();
            return new VehicleEnrichmentLogic(new StubFactory(handler), new MemoryCache(new MemoryCacheOptions()), config, NullLogger<IVehicleEnrichmentLogic>.Instance);
        }
        private static StubHandler Json(string json) => new StubHandler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) } };

        [Fact]
        public void Parse_Motorcycle_MapsFields()
        {
            var result = VehicleEnrichmentLogic.ParseVpicResponse(MotorcycleVin, Fixture("vpic_motorcycle.json"));
            Assert.NotNull(result);
            Assert.Equal("BMW", result!.Make);
            Assert.Equal("M 1000 R", result.Model);
            Assert.Equal(2024, result.Year);
            Assert.True(result.IsMotorcycle);
            Assert.Equal("Gasoline", result.FuelType);
            Assert.Equal(999, result.DisplacementCC);
            Assert.Equal(4, result.Cylinders);
            Assert.Equal(205, result.HorsePower);
            Assert.Equal("Water", result.Cooling);
            Assert.Equal("6-speed Manual/Standard", result.Transmission);
        }
        [Fact]
        public void Parse_Car_TitleCasesMakeAndWarnsOnBadCheckDigit()
        {
            var result = VehicleEnrichmentLogic.ParseVpicResponse(CarVin, Fixture("vpic_car.json"));
            Assert.NotNull(result);
            Assert.Equal("Honda", result!.Make);
            Assert.Equal("Accord", result.Model);
            Assert.False(result.IsMotorcycle);
            Assert.False(result.CheckDigitValid);
            Assert.Contains(result.Warnings, w => w.Contains("check digit"));
        }
        [Fact]
        public void Parse_NothingDecoded_ReturnsNull()
        {
            Assert.Null(VehicleEnrichmentLogic.ParseVpicResponse(CarVin, "{\"Results\":[{\"Make\":\"\",\"Model\":\"\"}]}"));
            Assert.Null(VehicleEnrichmentLogic.ParseVpicResponse(CarVin, "{\"Results\":[]}"));
        }
        [Theory]
        [InlineData("BMW", "BMW")]
        [InlineData("HONDA", "Honda")]
        [InlineData("HARLEY-DAVIDSON", "Harley-Davidson")]
        [InlineData("Ducati", "Ducati")]
        public void NormalizeMake(string input, string expected)
        {
            Assert.Equal(expected, VehicleEnrichmentLogic.NormalizeMake(input));
        }
        [Fact]
        public async Task DecodeVin_InvalidFormat_FailsWithoutCallingApi()
        {
            var handler = Json("{}");
            var result = await Build(handler).DecodeVin("not-a-vin");
            Assert.False(result.Success);
            Assert.Equal(0, handler.Calls);
        }
        [Fact]
        public async Task DecodeVin_Success_ReturnsResultAndCaches()
        {
            var handler = Json(Fixture("vpic_motorcycle.json"));
            var logic = Build(handler);
            var first = await logic.DecodeVin(MotorcycleVin.ToLower());
            var second = await logic.DecodeVin(MotorcycleVin);
            Assert.True(first.Success);
            Assert.Equal("M 1000 R", ((VinDecodeResult)first.AdditionalData!).Model);
            Assert.True(second.Success);
            Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task DecodeVin_ServerError_FailsGracefully()
        {
            var handler = new StubHandler { Respond = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) };
            var result = await Build(handler).DecodeVin(CarVin);
            Assert.False(result.Success);
            Assert.Contains("manually", result.Message);
        }
        [Fact]
        public async Task DecodeVin_UndecodableVin_Fails()
        {
            var result = await Build(Json("{\"Results\":[{\"Make\":\"\",\"Model\":\"\"}]}")).DecodeVin(CarVin);
            Assert.False(result.Success);
        }
        [Fact]
        public async Task GetMakes_NormalizesDedupesAndCaches()
        {
            var handler = Json("{\"Results\":[{\"MakeName\":\"HONDA\"},{\"MakeName\":\"BMW\"},{\"MakeName\":\"HONDA\"},{\"MakeName\":\"HARLEY-DAVIDSON\"}]}");
            var logic = Build(handler);
            var first = await logic.GetMakes(true);
            await logic.GetMakes(true);
            Assert.True(first.Success);
            Assert.Equal(new List<string> { "BMW", "Harley-Davidson", "Honda" }, (List<string>)first.AdditionalData!);
            Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task GetModels_ReturnsSortedModels()
        {
            var handler = Json("{\"Results\":[{\"Model_Name\":\"Civic\"},{\"Model_Name\":\"Accord\"}]}");
            var result = await Build(handler).GetModels("Honda", 2016, false);
            Assert.Equal(new List<string> { "Accord", "Civic" }, (List<string>)result.AdditionalData!);
        }
        [Theory]
        [InlineData("", 2016)]
        [InlineData("Honda", 1800)]
        public async Task GetModels_RejectsMissingInput(string make, int year)
        {
            var handler = Json("{}");
            var result = await Build(handler).GetModels(make, year, false);
            Assert.False(result.Success);
            Assert.Equal(0, handler.Calls);
        }
        [Fact]
        public async Task GetMakes_ServerError_FailsGracefully()
        {
            var handler = new StubHandler { Respond = () => new HttpResponseMessage(HttpStatusCode.BadGateway) };
            Assert.False((await Build(handler).GetMakes(false)).Success);
        }
    }
}
