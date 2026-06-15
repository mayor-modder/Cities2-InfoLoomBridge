using System;
using System.Collections.Generic;
using System.Reflection;

namespace InfoLoomBridge.InfoLoom
{
    public sealed class InfoLoomAdapter : IInfoLoomAdapter
    {
        private static readonly string[] PopulationStructureTypeNames =
        {
            "InfoLoom.Systems.PopulationStructureUISystem",
            "InfoLoomTwo.Systems.DemographicsData.Demographics"
        };

        private static readonly string[] WorkforceTypeNames =
        {
            "InfoLoom.Systems.WorkforceInfoLoomUISystem",
            "InfoLoomTwo.Systems.WorkforceData.WorkforceSystem"
        };

        private static readonly string[] WorkplacesTypeNames =
        {
            "InfoLoom.Systems.WorkplacesInfoLoomUISystem",
            "InfoLoomTwo.Systems.WorkplacesData.WorkplacesSystem"
        };

        private const string WorldTypeName = "Unity.Entities.World";
        private const string DefaultWorldMemberName = "DefaultGameObjectInjectionWorld";
        private const string ExistingSystemMemberName = "GetExistingSystemManaged(Type)";
        private const string PanelVisibleMemberName = "IsPanelVisible";
        private const string ForceUpdateOnceMemberName = "ForceUpdateOnce";
        private const string DemographicsUpdateMemberName = "UpdateDemographics";
        private const string OnUpdateMemberName = "OnUpdate";
        private const string TotalsMemberName = "m_Totals";
        private const string ResultsMemberName = "m_Results";

        private readonly IInfoLoomSystemResolver _systemResolver;

        public InfoLoomAdapter()
            : this(new ProductionInfoLoomSystemResolver())
        {
        }

        internal InfoLoomAdapter(IInfoLoomSystemResolver systemResolver)
        {
            _systemResolver = systemResolver ?? throw new ArgumentNullException(nameof(systemResolver));
        }

        public InfoLoomFirstPanelSliceResult ReadFirstPanelSlice()
        {
            if (!TryResolveSystem(_systemResolver.TryGetPopulationStructureSystem(), out object? populationStructure, out InfoLoomCompatibilityFailure? populationFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(populationFailure!);
            }

            if (!TryPrepareSystemForRead(populationStructure!, out InfoLoomCompatibilityFailure? populationPrepareFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(populationPrepareFailure!);
            }

            if (!TryReadRequiredField(populationStructure!, TotalsMemberName, out object? totals, out InfoLoomCompatibilityFailure? totalsFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(totalsFailure!);
            }

            if (!TryReadRequiredField(populationStructure!, ResultsMemberName, out object? demographicsResults, out InfoLoomCompatibilityFailure? demographicsFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(demographicsFailure!);
            }

            if (!TryResolveSystem(_systemResolver.TryGetWorkforceSystem(), out object? workforceSystem, out InfoLoomCompatibilityFailure? workforceSystemFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workforceSystemFailure!);
            }

            if (!TryPrepareSystemForRead(workforceSystem!, out InfoLoomCompatibilityFailure? workforcePrepareFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workforcePrepareFailure!);
            }

            if (!TryReadRequiredField(workforceSystem!, ResultsMemberName, out object? workforceResults, out InfoLoomCompatibilityFailure? workforceResultsFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workforceResultsFailure!);
            }

            if (!TryResolveSystem(_systemResolver.TryGetWorkplacesSystem(), out object? workplacesSystem, out InfoLoomCompatibilityFailure? workplacesSystemFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workplacesSystemFailure!);
            }

            if (!TryPrepareSystemForRead(workplacesSystem!, out InfoLoomCompatibilityFailure? workplacesPrepareFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workplacesPrepareFailure!);
            }

            if (!TryReadRequiredField(workplacesSystem!, ResultsMemberName, out object? workplacesResults, out InfoLoomCompatibilityFailure? workplacesResultsFailure))
            {
                return InfoLoomFirstPanelSliceResult.CreateFailure(workplacesResultsFailure!);
            }

            return InfoLoomFirstPanelSliceResult.CreateSuccess(
                new InfoLoomPanelSlice(
                    CreateTwoFieldPayload(TotalsMemberName, totals, ResultsMemberName, demographicsResults),
                    CreateSingleFieldPayload(ResultsMemberName, workforceResults),
                    CreateSingleFieldPayload(ResultsMemberName, workplacesResults)));
        }

        private static bool TryResolveSystem(InfoLoomSystemLookupResult lookupResult, out object? system, out InfoLoomCompatibilityFailure? failure)
        {
            if (lookupResult == null)
            {
                throw new ArgumentNullException(nameof(lookupResult));
            }

            system = lookupResult.System;
            failure = lookupResult.Failure;
            return lookupResult.IsSuccess && system != null;
        }

        private static bool TryPrepareSystemForRead(object instance, out InfoLoomCompatibilityFailure? failure)
        {
            string typeName = GetTypeName(instance);

            if (!TrySetBooleanProperty(instance, PanelVisibleMemberName, true, typeName, out failure))
            {
                return false;
            }

            if (!TryInvokeOptionalMethod(instance, ForceUpdateOnceMemberName, typeName, out failure))
            {
                return false;
            }

            if (TryGetMethod(instance, DemographicsUpdateMemberName, out MethodInfo? updateDemographics))
            {
                return TryInvokeMethod(instance, updateDemographics!, typeName, out failure);
            }

            if (TryGetMethod(instance, OnUpdateMemberName, out MethodInfo? onUpdate))
            {
                return TryInvokeMethod(instance, onUpdate!, typeName, out failure);
            }

            failure = null;
            return true;
        }

        private static bool TryReadRequiredField(
            object instance,
            string memberName,
            out object? value,
            out InfoLoomCompatibilityFailure? failure)
        {
            string typeName = GetTypeName(instance);
            FieldInfo? field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                value = null;
                failure = InfoLoomCompatibilityFailure.MissingMember(typeName, memberName);
                return false;
            }

            try
            {
                value = field.GetValue(instance);
            }
            catch (TargetException ex)
            {
                value = null;
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }
            catch (MemberAccessException ex)
            {
                value = null;
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }
            catch (ArgumentException ex)
            {
                value = null;
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                value = null;
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }

            if (value == null)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, $"{memberName} resolved to null", memberName);
                return false;
            }

            failure = null;
            return true;
        }

        private static bool TrySetBooleanProperty(
            object instance,
            string memberName,
            bool value,
            string typeName,
            out InfoLoomCompatibilityFailure? failure)
        {
            PropertyInfo? property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                failure = null;
                return true;
            }

            if (property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, $"{memberName} is not a writable boolean property", memberName);
                return false;
            }

            try
            {
                property.SetValue(instance, value);
            }
            catch (TargetInvocationException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.InnerException?.Message ?? ex.Message, memberName);
                return false;
            }
            catch (MemberAccessException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }
            catch (ArgumentException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, memberName);
                return false;
            }

            failure = null;
            return true;
        }

        private static bool TryInvokeOptionalMethod(
            object instance,
            string memberName,
            string typeName,
            out InfoLoomCompatibilityFailure? failure)
        {
            if (!TryGetMethod(instance, memberName, out MethodInfo? method))
            {
                failure = null;
                return true;
            }

            return TryInvokeMethod(instance, method!, typeName, out failure);
        }

        private static bool TryGetMethod(object instance, string memberName, out MethodInfo? method)
        {
            method = instance.GetType().GetMethod(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
            return method != null;
        }

        private static bool TryInvokeMethod(object instance, MethodInfo method, string typeName, out InfoLoomCompatibilityFailure? failure)
        {
            try
            {
                method.Invoke(instance, Array.Empty<object>());
            }
            catch (TargetInvocationException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.InnerException?.Message ?? ex.Message, method.Name);
                return false;
            }
            catch (MemberAccessException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, method.Name);
                return false;
            }
            catch (ArgumentException ex)
            {
                failure = InfoLoomCompatibilityFailure.InvalidPayload(typeName, ex.Message, method.Name);
                return false;
            }

            failure = null;
            return true;
        }

        private static string GetTypeName(object instance)
        {
            return instance.GetType().FullName ?? instance.GetType().Name;
        }

        private static IReadOnlyDictionary<string, object?> CreateTwoFieldPayload(
            string firstName,
            object? firstValue,
            string secondName,
            object? secondValue)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [NormalizeFieldName(firstName)] = firstValue,
                [NormalizeFieldName(secondName)] = secondValue
            };
        }

        private static IReadOnlyDictionary<string, object?> CreateSingleFieldPayload(string name, object? value)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [NormalizeFieldName(name)] = value
            };
        }

        private static string NormalizeFieldName(string fieldName)
        {
            return fieldName switch
            {
                TotalsMemberName => "totals",
                ResultsMemberName => "results",
                _ => fieldName
            };
        }

        internal sealed class ProductionInfoLoomSystemResolver : IInfoLoomSystemResolver
        {
            private readonly IInfoLoomReflectionProbe _probe;

            internal ProductionInfoLoomSystemResolver()
                : this(new DefaultReflectionProbe())
            {
            }

            internal ProductionInfoLoomSystemResolver(IInfoLoomReflectionProbe probe)
            {
                _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            }

            public InfoLoomSystemLookupResult TryGetPopulationStructureSystem()
            {
                return TryGetSystem(PopulationStructureTypeNames);
            }

            public InfoLoomSystemLookupResult TryGetWorkforceSystem()
            {
                return TryGetSystem(WorkforceTypeNames);
            }

            public InfoLoomSystemLookupResult TryGetWorkplacesSystem()
            {
                return TryGetSystem(WorkplacesTypeNames);
            }

            private InfoLoomSystemLookupResult TryGetSystem(string[] typeNames)
            {
                if (typeNames == null || typeNames.Length == 0)
                {
                    throw new ArgumentException("At least one system type name must be provided.", nameof(typeNames));
                }

                Type? systemType = null;
                string typeName = typeNames[0];
                foreach (string candidateTypeName in typeNames)
                {
                    if (_probe.TryFindLoadedType(candidateTypeName, out systemType) && systemType != null)
                    {
                        typeName = candidateTypeName;
                        break;
                    }
                }

                if (systemType == null)
                {
                    return InfoLoomSystemLookupResult.CreateFailure(InfoLoomCompatibilityFailure.MissingSystem(typeName));
                }

                if (!_probe.TryGetDefaultWorld(out object? world, out InfoLoomCompatibilityFailure? worldFailure))
                {
                    return InfoLoomSystemLookupResult.CreateFailure(worldFailure ?? InfoLoomCompatibilityFailure.MissingSystem(typeName));
                }

                if (world == null)
                {
                    return InfoLoomSystemLookupResult.CreateFailure(InfoLoomCompatibilityFailure.MissingSystem(typeName));
                }

                if (!_probe.TryGetExistingSystemManaged(world, systemType, out object? system, out InfoLoomCompatibilityFailure? systemFailure))
                {
                    return InfoLoomSystemLookupResult.CreateFailure(systemFailure ?? InfoLoomCompatibilityFailure.MissingSystem(typeName));
                }

                if (system == null)
                {
                    return InfoLoomSystemLookupResult.CreateFailure(InfoLoomCompatibilityFailure.MissingSystem(typeName));
                }

                return InfoLoomSystemLookupResult.Success(system);
            }
        }

        internal sealed class DefaultReflectionProbe : IInfoLoomReflectionProbe
        {
            public bool TryFindLoadedType(string fullName, out Type? type)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? candidate = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (candidate != null)
                    {
                        type = candidate;
                        return true;
                    }
                }

                type = Type.GetType(fullName, throwOnError: false, ignoreCase: false);
                return type != null;
            }

            public bool TryGetDefaultWorld(out object? world, out InfoLoomCompatibilityFailure? failure)
            {
                if (!TryFindLoadedType(WorldTypeName, out Type? worldType) || worldType == null)
                {
                    world = null;
                    failure = InfoLoomCompatibilityFailure.MissingMember(WorldTypeName, DefaultWorldMemberName);
                    return false;
                }

                PropertyInfo? property = worldType.GetProperty(DefaultWorldMemberName, BindingFlags.Static | BindingFlags.Public);
                if (property == null)
                {
                    world = null;
                    failure = InfoLoomCompatibilityFailure.MissingMember(WorldTypeName, DefaultWorldMemberName);
                    return false;
                }

                try
                {
                    world = property.GetValue(null);
                }
                catch (TargetInvocationException ex)
                {
                    world = null;
                    failure = InfoLoomCompatibilityFailure.InvalidPayload(
                        WorldTypeName,
                        ex.InnerException?.Message ?? ex.Message,
                        DefaultWorldMemberName);
                    return false;
                }
                catch (MemberAccessException ex)
                {
                    world = null;
                    failure = InfoLoomCompatibilityFailure.InvalidPayload(WorldTypeName, ex.Message, DefaultWorldMemberName);
                    return false;
                }

                failure = null;
                return true;
            }

            public bool TryGetExistingSystemManaged(object world, Type systemType, out object? system, out InfoLoomCompatibilityFailure? failure)
            {
                MethodInfo? getter = world.GetType().GetMethod(
                    "GetExistingSystemManaged",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(Type) },
                    modifiers: null);
                if (getter == null)
                {
                    system = null;
                    failure = InfoLoomCompatibilityFailure.MissingMember(WorldTypeName, ExistingSystemMemberName);
                    return false;
                }

                try
                {
                    system = getter.Invoke(world, new object[] { systemType });
                }
                catch (TargetInvocationException ex)
                {
                    system = null;
                    failure = InfoLoomCompatibilityFailure.InvalidPayload(
                        systemType.FullName ?? systemType.Name,
                        ex.InnerException?.Message ?? ex.Message,
                        ExistingSystemMemberName);
                    return false;
                }
                catch (MemberAccessException ex)
                {
                    system = null;
                    failure = InfoLoomCompatibilityFailure.InvalidPayload(
                        systemType.FullName ?? systemType.Name,
                        ex.Message,
                        ExistingSystemMemberName);
                    return false;
                }
                catch (ArgumentException ex)
                {
                    system = null;
                    failure = InfoLoomCompatibilityFailure.InvalidPayload(
                        systemType.FullName ?? systemType.Name,
                        ex.Message,
                        ExistingSystemMemberName);
                    return false;
                }

                failure = null;
                return true;
            }
        }
    }
}
