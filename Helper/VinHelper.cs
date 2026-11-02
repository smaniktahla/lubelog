using System.Text.RegularExpressions;

namespace CarCareTracker.Helper
{
    public static class VinHelper
    {
        private static readonly Regex VinFormat = new Regex("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.Compiled);
        public static string Normalize(string? vin)
        {
            return (vin ?? string.Empty).Trim().ToUpperInvariant();
        }
        /// <summary>
        /// 17 characters, letters I, O and Q are not allowed.
        /// </summary>
        public static bool IsValidFormat(string? vin)
        {
            return VinFormat.IsMatch(Normalize(vin));
        }
        /// <summary>
        /// ISO 3779 / 49 CFR 565 check digit (position 9). Only mandatory for North American VINs.
        /// </summary>
        public static bool HasValidCheckDigit(string? vin)
        {
            vin = Normalize(vin);
            if (!IsValidFormat(vin))
            {
                return false;
            }
            int[] weights = { 8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum = 0;
            for (int i = 0; i < 17; i++)
            {
                sum += Transliterate(vin[i]) * weights[i];
            }
            int remainder = sum % 11;
            char expected = remainder == 10 ? 'X' : (char)('0' + remainder);
            return vin[8] == expected;
        }
        private static int Transliterate(char c)
        {
            if (char.IsDigit(c))
            {
                return c - '0';
            }
            return c switch
            {
                'A' or 'J' => 1,
                'B' or 'K' or 'S' => 2,
                'C' or 'L' or 'T' => 3,
                'D' or 'M' or 'U' => 4,
                'E' or 'N' or 'V' => 5,
                'F' or 'W' => 6,
                'G' or 'P' or 'X' => 7,
                'H' or 'Y' => 8,
                'R' or 'Z' => 9,
                _ => 0
            };
        }
    }
}
