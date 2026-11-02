namespace CarCareTracker.Models
{
    /// <summary>
    /// One maintenance spec with the most recent service record that touched it.
    /// </summary>
    public class SpecHistoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool HasHistory { get; set; }
        public DateTime LastDate { get; set; }
        public int LastMileage { get; set; }
        public string LastDescription { get; set; } = string.Empty;
        /// <summary>
        /// Distance since the last change, 0 when unknown.
        /// </summary>
        public int DistanceSince { get; set; }
    }
}
