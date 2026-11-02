using CarCareTracker.Helper;
using Xunit;

namespace CarCareTracker.Tests
{
    public class VinHelperTests
    {
        //public example VIN with a valid check digit.
        private const string ValidVin = "1M8GDM9AXKP042788";

        [Fact]
        public void Normalize_TrimsAndUpperCases()
        {
            Assert.Equal(ValidVin, VinHelper.Normalize("  1m8gdm9axkp042788 "));
        }
        [Theory]
        [InlineData("1M8GDM9AXKP042788", true)]
        [InlineData("1M8GDM9AXKP04278", false)]   //16 chars
        [InlineData("1M8GDM9AXKP0427888", false)] //18 chars
        [InlineData("1M8GDM9AXKP04278I", false)]  //I not allowed
        [InlineData("1M8GDM9AXKP04278O", false)]  //O not allowed
        [InlineData("1M8GDM9AXKP04278Q", false)]  //Q not allowed
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidFormat(string? vin, bool expected)
        {
            Assert.Equal(expected, VinHelper.IsValidFormat(vin));
        }
        [Fact]
        public void HasValidCheckDigit_AcceptsKnownGoodVin()
        {
            Assert.True(VinHelper.HasValidCheckDigit(ValidVin));
        }
        [Fact]
        public void HasValidCheckDigit_RejectsTamperedVin()
        {
            Assert.False(VinHelper.HasValidCheckDigit("1M8GDM9A1KP042788"));
        }
        [Fact]
        public void HasValidCheckDigit_RejectsBadFormat()
        {
            Assert.False(VinHelper.HasValidCheckDigit("short"));
        }
    }
}
