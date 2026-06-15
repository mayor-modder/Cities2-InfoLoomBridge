using System;
using System.Collections;
using System.Collections.Generic;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;

namespace InfoLoomBridge.Export
{
    public sealed class BridgeSnapshot
    {
        public string ExportVersion { get; set; } = string.Empty;

        public DateTimeOffset GeneratedAt { get; set; }

        public string BridgeVersion { get; set; } = string.Empty;

        public string InfoLoomVersion { get; set; } = string.Empty;

        public InfoLoomBuildFingerprint? InfoLoomBuild { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Message { get; set; }

        public BridgePanelsSnapshot Panels { get; set; } = new BridgePanelsSnapshot();

        public BridgeExtensionsSnapshot BridgeExtensions { get; set; } = new BridgeExtensionsSnapshot();

        public static BridgeSnapshot FromCompatibility(InfoLoomCompatibilityReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return new BridgeSnapshot
            {
                InfoLoomVersion = report.DetectedVersion?.ToString() ?? string.Empty,
                InfoLoomBuild = report.BuildFingerprint,
                Status = report.StatusText
            };
        }

        public BridgeSnapshot Copy()
        {
            return new BridgeSnapshot
            {
                ExportVersion = ExportVersion,
                GeneratedAt = GeneratedAt,
                BridgeVersion = BridgeVersion,
                InfoLoomVersion = InfoLoomVersion,
                InfoLoomBuild = CloneBuildFingerprint(InfoLoomBuild),
                Status = Status,
                Message = Message,
                Panels = new BridgePanelsSnapshot
                {
                    Demographics = ClonePayload(Panels?.Demographics),
                    Workforce = ClonePayload(Panels?.Workforce),
                    Workplaces = ClonePayload(Panels?.Workplaces)
                },
                BridgeExtensions = CloneBridgeExtensions(BridgeExtensions)
            };
        }

        private static InfoLoomBuildFingerprint? CloneBuildFingerprint(InfoLoomBuildFingerprint? fingerprint)
        {
            if (fingerprint == null)
            {
                return null;
            }

            return new InfoLoomBuildFingerprint(
                fingerprint.Location,
                fingerprint.AssemblyVersion,
                fingerprint.ProductVersion,
                fingerprint.GitCommit,
                fingerprint.FileLastWriteUtc);
        }

        private static BridgeExtensionsSnapshot CloneBridgeExtensions(BridgeExtensionsSnapshot? extensions)
        {
            return new BridgeExtensionsSnapshot
            {
                CommuteDestinations = CloneCommuteDestinations(extensions?.CommuteDestinations)
            };
        }

        private static CommuteDestinationsExtensionSnapshot? CloneCommuteDestinations(CommuteDestinationsExtensionSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new CommuteDestinationsExtensionSnapshot
            {
                Status = snapshot.Status,
                Message = snapshot.Message,
                SourceComponent = snapshot.SourceComponent,
                Notes = (string[])snapshot.Notes.Clone(),
                ByDistrict = CloneDistrictRows(snapshot.ByDistrict),
                TopWorkProviders = CloneProviderRows(snapshot.TopWorkProviders),
                ProviderRowsTotal = snapshot.ProviderRowsTotal,
                ProviderRowsExported = snapshot.ProviderRowsExported,
                ProviderRowsTruncated = snapshot.ProviderRowsTruncated
            };
        }

        private static CommuteDestinationDistrictRow[] CloneDistrictRows(CommuteDestinationDistrictRow[] rows)
        {
            var clone = new CommuteDestinationDistrictRow[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                CommuteDestinationDistrictRow row = rows[i];
                clone[i] = new CommuteDestinationDistrictRow
                {
                    DistrictEntity = row.DistrictEntity,
                    DistrictName = row.DistrictName,
                    ProviderCount = row.ProviderCount,
                    JobsTotal = row.JobsTotal,
                    JobsFilled = row.JobsFilled,
                    JobsOpen = row.JobsOpen,
                    CommuterEmployees = row.CommuterEmployees,
                    LocalEmployees = row.LocalEmployees,
                    SectorCommuterEmployees = CloneSectorValues(row.SectorCommuterEmployees)
                };
            }

            return clone;
        }

        private static CommuteDestinationProviderRow[] CloneProviderRows(CommuteDestinationProviderRow[] rows)
        {
            var clone = new CommuteDestinationProviderRow[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                CommuteDestinationProviderRow row = rows[i];
                clone[i] = new CommuteDestinationProviderRow
                {
                    ProviderEntity = row.ProviderEntity,
                    BuildingEntity = row.BuildingEntity,
                    DistrictEntity = row.DistrictEntity,
                    DistrictName = row.DistrictName,
                    BuildingName = row.BuildingName,
                    CompanyName = row.CompanyName,
                    Sector = row.Sector,
                    JobsTotal = row.JobsTotal,
                    JobsFilled = row.JobsFilled,
                    JobsOpen = row.JobsOpen,
                    CommuterEmployees = row.CommuterEmployees,
                    LocalEmployees = row.LocalEmployees
                };
            }

            return clone;
        }

        private static SectorIntValueSet CloneSectorValues(SectorIntValueSet? values)
        {
            if (values == null)
            {
                return new SectorIntValueSet();
            }

            return new SectorIntValueSet
            {
                Service = values.Service,
                Commercial = values.Commercial,
                Leisure = values.Leisure,
                Extractor = values.Extractor,
                Industrial = values.Industrial,
                Office = values.Office
            };
        }

        private static object? ClonePayload(object? payload)
        {
            switch (payload)
            {
                case null:
                    return null;
                case IDictionary<string, object?> mutableDictionary:
                    return CloneDictionary(mutableDictionary);
                case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                    return CloneDictionary(readOnlyDictionary);
                case IEnumerable enumerable when payload is not string:
                    return CloneEnumerable(enumerable);
                default:
                    return payload;
            }
        }

        private static Dictionary<string, object?> CloneDictionary(IEnumerable<KeyValuePair<string, object?>> source)
        {
            var clone = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> entry in source)
            {
                clone[entry.Key] = ClonePayload(entry.Value);
            }

            return clone;
        }

        private static List<object?> CloneEnumerable(IEnumerable source)
        {
            var clone = new List<object?>();
            foreach (object? item in source)
            {
                clone.Add(ClonePayload(item));
            }

            return clone;
        }
    }

    public sealed class BridgePanelsSnapshot
    {
        public object? Demographics { get; set; }

        public object? Workforce { get; set; }

        public object? Workplaces { get; set; }
    }

    public sealed class BridgeExtensionsSnapshot
    {
        public CommuteDestinationsExtensionSnapshot? CommuteDestinations { get; set; }
    }

    public sealed class CommuteDestinationsExtensionSnapshot
    {
        public string Status { get; set; } = string.Empty;

        public string? Message { get; set; }

        public string SourceComponent { get; set; } = string.Empty;

        public string[] Notes { get; set; } = Array.Empty<string>();

        public CommuteDestinationDistrictRow[] ByDistrict { get; set; } = Array.Empty<CommuteDestinationDistrictRow>();

        public CommuteDestinationProviderRow[] TopWorkProviders { get; set; } = Array.Empty<CommuteDestinationProviderRow>();

        public int ProviderRowsTotal { get; set; }

        public int ProviderRowsExported { get; set; }

        public bool ProviderRowsTruncated { get; set; }
    }
}
