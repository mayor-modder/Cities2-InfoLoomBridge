using System;
using System.Collections.Generic;

namespace InfoLoomBridge.InfoLoom
{
    public interface IInfoLoomAdapter
    {
        InfoLoomFirstPanelSliceResult ReadFirstPanelSlice();
    }

    internal interface IInfoLoomSystemResolver
    {
        InfoLoomSystemLookupResult TryGetPopulationStructureSystem();

        InfoLoomSystemLookupResult TryGetWorkforceSystem();

        InfoLoomSystemLookupResult TryGetWorkplacesSystem();
    }

    internal interface IInfoLoomReflectionProbe
    {
        bool TryFindLoadedType(string fullName, out Type? type);

        bool TryGetDefaultWorld(out object? world, out InfoLoomCompatibilityFailure? failure);

        bool TryGetExistingSystemManaged(
            object world,
            Type systemType,
            out object? system,
            out InfoLoomCompatibilityFailure? failure);
    }

    internal sealed class InfoLoomSystemLookupResult
    {
        private InfoLoomSystemLookupResult(object? system, InfoLoomCompatibilityFailure? failure)
        {
            System = system;
            Failure = failure;
        }

        public bool IsSuccess => Failure == null;

        public object? System { get; }

        public InfoLoomCompatibilityFailure? Failure { get; }

        public static InfoLoomSystemLookupResult Success(object system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            return new InfoLoomSystemLookupResult(system, failure: null);
        }

        public static InfoLoomSystemLookupResult CreateFailure(InfoLoomCompatibilityFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new InfoLoomSystemLookupResult(system: null, failure);
        }
    }

    public enum InfoLoomCompatibilityFailureKind
    {
        MissingSystem = 0,
        MissingMember = 1,
        InvalidPayload = 2
    }

    public sealed class InfoLoomCompatibilityFailure
    {
        private InfoLoomCompatibilityFailure(
            InfoLoomCompatibilityFailureKind kind,
            string typeName,
            string? memberName,
            string message,
            string? detail)
        {
            Kind = kind;
            TypeName = typeName;
            MemberName = memberName;
            Message = message;
            Detail = detail;
        }

        public InfoLoomCompatibilityFailureKind Kind { get; }

        public string TypeName { get; }

        public string? MemberName { get; }

        public string Message { get; }

        public string? Detail { get; }

        public static InfoLoomCompatibilityFailure MissingSystem(string typeName)
        {
            return new InfoLoomCompatibilityFailure(
                InfoLoomCompatibilityFailureKind.MissingSystem,
                typeName,
                memberName: null,
                message: $"InfoLoom system type not available: {typeName}",
                detail: null);
        }

        public static InfoLoomCompatibilityFailure MissingMember(string typeName, string memberName)
        {
            return new InfoLoomCompatibilityFailure(
                InfoLoomCompatibilityFailureKind.MissingMember,
                typeName,
                memberName,
                message: $"InfoLoom member not available: {typeName}.{memberName}",
                detail: null);
        }

        public static InfoLoomCompatibilityFailure InvalidPayload(string typeName, string detail, string? memberName = null)
        {
            return new InfoLoomCompatibilityFailure(
                InfoLoomCompatibilityFailureKind.InvalidPayload,
                typeName,
                memberName,
                message: $"InfoLoom payload is not readable: {typeName}",
                detail);
        }
    }

    public sealed class InfoLoomFirstPanelSliceResult
    {
        private InfoLoomFirstPanelSliceResult(InfoLoomPanelSlice? panels, InfoLoomCompatibilityFailure? failure)
        {
            Panels = panels;
            Failure = failure;
        }

        public bool IsSuccess => Failure == null;

        public InfoLoomPanelSlice? Panels { get; }

        public InfoLoomCompatibilityFailure? Failure { get; }

        public static InfoLoomFirstPanelSliceResult CreateSuccess(InfoLoomPanelSlice panels)
        {
            if (panels == null)
            {
                throw new ArgumentNullException(nameof(panels));
            }

            return new InfoLoomFirstPanelSliceResult(panels, failure: null);
        }

        public static InfoLoomFirstPanelSliceResult CreateFailure(InfoLoomCompatibilityFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new InfoLoomFirstPanelSliceResult(panels: null, failure);
        }
    }

    public sealed class InfoLoomPanelSlice
    {
        public InfoLoomPanelSlice(
            IReadOnlyDictionary<string, object?> demographics,
            IReadOnlyDictionary<string, object?> workforce,
            IReadOnlyDictionary<string, object?> workplaces)
        {
            Demographics = demographics ?? throw new ArgumentNullException(nameof(demographics));
            Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
            Workplaces = workplaces ?? throw new ArgumentNullException(nameof(workplaces));
        }

        public IReadOnlyDictionary<string, object?> Demographics { get; }

        public IReadOnlyDictionary<string, object?> Workforce { get; }

        public IReadOnlyDictionary<string, object?> Workplaces { get; }
    }
}
