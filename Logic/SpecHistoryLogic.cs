using CarCareTracker.Models;

namespace CarCareTracker.Logic
{
    /// <summary>
    /// Works out "last changed / serviced" for each maintenance spec from existing service, repair and upgrade records.
    /// A record counts for a spec when it is tagged with the spec name, or its description contains one of the spec's keywords.
    /// Nothing is stored; it is computed whenever the vehicle page loads.
    /// </summary>
    public static class SpecHistoryLogic
    {
        private class Rule
        {
            public string[] Keywords { get; init; } = Array.Empty<string>();
            public string[] Excludes { get; init; } = Array.Empty<string>();
        }
        private static readonly string[] OilChange = { "oil change", "engine oil", "oil & filter", "oil and filter", "oil service" };
        private static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Oil Spec"] = new Rule { Keywords = OilChange, Excludes = new[] { "fork" } },
            ["Oil Filter"] = new Rule { Keywords = OilChange.Concat(new[] { "oil filter" }).ToArray(), Excludes = new[] { "fork" } },
            ["Air Filter"] = new Rule { Keywords = new[] { "air filter", "airfilter" }, Excludes = new[] { "cabin" } },
            ["Cabin Air Filter"] = new Rule { Keywords = new[] { "cabin" } },
            ["Spark Plugs"] = new Rule { Keywords = new[] { "spark plug", "sparkplug" } },
            ["Battery Form Factor"] = new Rule { Keywords = new[] { "battery" } },
            ["Tire Size"] = new Rule { Keywords = new[] { "tire", "tyre" } },
            ["Tire Size Front"] = new Rule { Keywords = new[] { "front tire", "front tyre" } },
            ["Tire Size Rear"] = new Rule { Keywords = new[] { "rear tire", "rear tyre" } },
            ["Coolant"] = new Rule { Keywords = new[] { "coolant", "antifreeze" } },
            ["Brake Fluid"] = new Rule { Keywords = new[] { "brake fluid", "brake flush" } },
            ["Transmission Fluid"] = new Rule { Keywords = new[] { "transmission fluid", "trans fluid", "atf" } },
            ["Chain"] = new Rule { Keywords = new[] { "chain" } },
            ["Front Sprocket"] = new Rule { Keywords = new[] { "front sprocket", "sprockets" } },
            ["Rear Sprocket"] = new Rule { Keywords = new[] { "rear sprocket", "sprockets" } },
            ["Valve Clearance"] = new Rule { Keywords = new[] { "valve clearance", "valve check", "valve adjust", "valve service" } },
            ["Fork Oil"] = new Rule { Keywords = new[] { "fork oil", "fork service" } }
        };
        /// <summary>
        /// Specs with a value or with matching history, in the order given. Specs the app can't track (e.g. Oil Capacity) show only their value.
        /// </summary>
        public static List<SpecHistoryItem> Build(IEnumerable<ExtraField> specs, IEnumerable<GenericRecord> records, int currentMileage)
        {
            var recordList = records.ToList();
            var items = new List<SpecHistoryItem>();
            foreach (var spec in specs)
            {
                var item = new SpecHistoryItem { Name = spec.Name, Value = spec.Value };
                var latest = Rules.TryGetValue(spec.Name, out var rule)
                    ? recordList.Where(x => Matches(x, spec.Name, rule)).OrderByDescending(x => x.Date).ThenByDescending(x => x.Mileage).FirstOrDefault()
                    : null;
                if (latest != null)
                {
                    item.HasHistory = true;
                    item.LastDate = latest.Date;
                    item.LastMileage = latest.Mileage;
                    item.LastDescription = latest.Description;
                    item.DistanceSince = latest.Mileage > 0 && currentMileage >= latest.Mileage ? currentMileage - latest.Mileage : 0;
                }
                if (item.HasHistory || !string.IsNullOrWhiteSpace(item.Value))
                {
                    items.Add(item);
                }
            }
            return items;
        }
        private static bool Matches(GenericRecord record, string specName, Rule rule)
        {
            if (record.Tags.Any(t => t.Equals(specName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            var description = record.Description ?? string.Empty;
            if (rule.Excludes.Any(x => description.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            return rule.Keywords.Any(x => description.Contains(x, StringComparison.OrdinalIgnoreCase));
        }
    }
}
