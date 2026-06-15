using System;
using System.Collections.Generic;
using System.Linq;

namespace InfoLoomBridge.Runtime
{
    public sealed class CommuteDestinationsCollector
    {
        private readonly IReadOnlyList<ProviderSnapshot> _providers;
        private readonly int _topProviderLimit;

        public CommuteDestinationsCollector(IReadOnlyList<ProviderSnapshot> providers, int topProviderLimit = 200)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _topProviderLimit = Math.Max(1, topProviderLimit);
        }

        public CommuteDestinationsCollectionResult Collect()
        {
            var districtRows = _providers
                .GroupBy(provider => new DistrictKey(provider.DistrictEntity, provider.DistrictName))
                .Select(group => CreateDistrictRow(group.Key, group))
                .OrderByDescending(row => row.CommuterEmployees)
                .ThenByDescending(row => row.JobsTotal)
                .ThenBy(row => row.DistrictName, StringComparer.Ordinal)
                .ToArray();

            CommuteDestinationProviderRow[] sortedProviders = _providers
                .Select(CreateProviderRow)
                .OrderByDescending(row => row.CommuterEmployees)
                .ThenByDescending(row => row.JobsTotal)
                .ThenBy(row => row.ProviderEntity)
                .ToArray();

            CommuteDestinationProviderRow[] exportedProviders = sortedProviders
                .Take(_topProviderLimit)
                .ToArray();

            return CommuteDestinationsCollectionResult.CreateSuccess(
                new CommuteDestinationsCollectedData
                {
                    ByDistrict = districtRows,
                    TopWorkProviders = exportedProviders,
                    ProviderRowsTotal = sortedProviders.Length,
                    ProviderRowsExported = exportedProviders.Length,
                    ProviderRowsTruncated = sortedProviders.Length > exportedProviders.Length
                });
        }

        private static CommuteDestinationDistrictRow CreateDistrictRow(
            DistrictKey key,
            IEnumerable<ProviderSnapshot> providers)
        {
            var row = new CommuteDestinationDistrictRow
            {
                DistrictEntity = key.DistrictEntity,
                DistrictName = key.DistrictName
            };

            foreach (ProviderSnapshot provider in providers)
            {
                row.ProviderCount++;
                row.JobsTotal += provider.JobsTotal;
                row.JobsFilled += provider.JobsFilled;
                row.JobsOpen += provider.JobsOpen;
                row.CommuterEmployees += provider.CommuterEmployees;
                row.LocalEmployees += provider.LocalEmployees;
                AddSectorCommute(row.SectorCommuterEmployees, provider.Sector, provider.CommuterEmployees);
            }

            return row;
        }

        private static CommuteDestinationProviderRow CreateProviderRow(ProviderSnapshot provider)
        {
            return new CommuteDestinationProviderRow
            {
                ProviderEntity = provider.ProviderEntity,
                BuildingEntity = provider.BuildingEntity,
                DistrictEntity = provider.DistrictEntity,
                DistrictName = provider.DistrictName,
                BuildingName = provider.BuildingName,
                Sector = provider.Sector,
                JobsTotal = provider.JobsTotal,
                JobsFilled = provider.JobsFilled,
                JobsOpen = provider.JobsOpen,
                CommuterEmployees = provider.CommuterEmployees,
                LocalEmployees = provider.LocalEmployees
            };
        }

        private static void AddSectorCommute(SectorIntValueSet sectorValues, string sector, int commuters)
        {
            switch (sector)
            {
                case "service":
                    sectorValues.Service += commuters;
                    break;
                case "commercial":
                    sectorValues.Commercial += commuters;
                    break;
                case "leisure":
                    sectorValues.Leisure += commuters;
                    break;
                case "extractor":
                    sectorValues.Extractor += commuters;
                    break;
                case "industrial":
                    sectorValues.Industrial += commuters;
                    break;
                case "office":
                    sectorValues.Office += commuters;
                    break;
            }
        }

        private readonly struct DistrictKey : IEquatable<DistrictKey>
        {
            public DistrictKey(int? districtEntity, string? districtName)
            {
                DistrictEntity = districtEntity;
                DistrictName = districtName ?? string.Empty;
            }

            public int? DistrictEntity { get; }

            public string DistrictName { get; }

            public bool Equals(DistrictKey other)
            {
                return DistrictEntity == other.DistrictEntity
                    && string.Equals(DistrictName, other.DistrictName, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is DistrictKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((DistrictEntity ?? 0) * 397) ^ DistrictName.GetHashCode();
                }
            }
        }
    }
}
