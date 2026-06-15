using System;

namespace InfoLoomBridge.Runtime
{
    public interface ICommuteDestinationsCollector
    {
        CommuteDestinationsCollectionResult Collect();
    }

    public static class CommuteDestinationConstants
    {
        public const string SourceComponent =
            "ecs.commute_destinations:Game.Companies.WorkProvider|Game.Companies.Employee|Game.Areas.CurrentDistrict|Game.Buildings.Building|Game.Prefabs.PrefabRef|Game.Prefabs.WorkplaceData|Game.Prefabs.IndustrialProcessData";

        public static readonly string[] DefaultNotes =
        {
            "commute destinations are bridge-owned runtime summaries built from active work providers and employee buffers",
            "outside-connection origin attribution is not included in this export"
        };
    }

    public sealed class CommuteDestinationsCollectedData
    {
        public CommuteDestinationDistrictRow[] ByDistrict { get; set; } = Array.Empty<CommuteDestinationDistrictRow>();

        public CommuteDestinationProviderRow[] TopWorkProviders { get; set; } = Array.Empty<CommuteDestinationProviderRow>();

        public string[] Notes { get; set; } = (string[])CommuteDestinationConstants.DefaultNotes.Clone();

        public int ProviderRowsTotal { get; set; }

        public int ProviderRowsExported { get; set; }

        public bool ProviderRowsTruncated { get; set; }
    }

    public sealed class CommuteDestinationsCollectionResult
    {
        private CommuteDestinationsCollectionResult(CommuteDestinationsCollectedData? value, string? errorMessage)
        {
            Value = value;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess => ErrorMessage == null;

        public CommuteDestinationsCollectedData? Value { get; }

        public string? ErrorMessage { get; }

        public static CommuteDestinationsCollectionResult CreateSuccess(CommuteDestinationsCollectedData value)
        {
            return new CommuteDestinationsCollectionResult(value, errorMessage: null);
        }

        public static CommuteDestinationsCollectionResult CreateFailure(string errorMessage)
        {
            return new CommuteDestinationsCollectionResult(value: null, errorMessage);
        }
    }

    public sealed class ProviderSnapshot
    {
        public ProviderSnapshot(
            int providerEntity,
            int? buildingEntity,
            int? districtEntity,
            string? districtName,
            string? buildingName,
            string sector,
            int jobsTotal,
            int jobsFilled,
            int commuterEmployees,
            int localEmployees)
        {
            ProviderEntity = providerEntity;
            BuildingEntity = buildingEntity;
            DistrictEntity = districtEntity;
            DistrictName = districtName;
            BuildingName = buildingName;
            Sector = sector;
            JobsTotal = jobsTotal;
            JobsFilled = jobsFilled;
            CommuterEmployees = commuterEmployees;
            LocalEmployees = localEmployees;
        }

        public int ProviderEntity { get; }

        public int? BuildingEntity { get; }

        public int? DistrictEntity { get; }

        public string? DistrictName { get; }

        public string? BuildingName { get; }

        public string Sector { get; }

        public int JobsTotal { get; }

        public int JobsFilled { get; }

        public int JobsOpen => JobsTotal - JobsFilled;

        public int CommuterEmployees { get; }

        public int LocalEmployees { get; }
    }

    public sealed class NullCommuteDestinationsCollector : ICommuteDestinationsCollector
    {
        private readonly string _errorMessage;

        public NullCommuteDestinationsCollector(string errorMessage = "Commute destinations collector is not configured.")
        {
            _errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "Commute destinations collector is not configured."
                : errorMessage;
        }

        public CommuteDestinationsCollectionResult Collect()
        {
            return CommuteDestinationsCollectionResult.CreateFailure(_errorMessage);
        }
    }

    public sealed class SectorIntValueSet
    {
        public int Service { get; set; }

        public int Commercial { get; set; }

        public int Leisure { get; set; }

        public int Extractor { get; set; }

        public int Industrial { get; set; }

        public int Office { get; set; }
    }

    public sealed class CommuteDestinationDistrictRow
    {
        public int? DistrictEntity { get; set; }

        public string? DistrictName { get; set; }

        public int ProviderCount { get; set; }

        public int JobsTotal { get; set; }

        public int JobsFilled { get; set; }

        public int JobsOpen { get; set; }

        public int CommuterEmployees { get; set; }

        public int LocalEmployees { get; set; }

        public SectorIntValueSet SectorCommuterEmployees { get; set; } = new SectorIntValueSet();
    }

    public sealed class CommuteDestinationProviderRow
    {
        public int ProviderEntity { get; set; }

        public int? BuildingEntity { get; set; }

        public int? DistrictEntity { get; set; }

        public string? DistrictName { get; set; }

        public string? BuildingName { get; set; }

        public string? CompanyName { get; set; }

        public string Sector { get; set; } = string.Empty;

        public int JobsTotal { get; set; }

        public int JobsFilled { get; set; }

        public int JobsOpen { get; set; }

        public int CommuterEmployees { get; set; }

        public int LocalEmployees { get; set; }
    }
}
