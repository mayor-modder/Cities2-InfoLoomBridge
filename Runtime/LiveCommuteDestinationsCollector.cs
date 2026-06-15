using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.UI;
using Game.UI.InGame;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace InfoLoomBridge.Runtime
{
    public sealed class LiveCommuteDestinationsCollector : ICommuteDestinationsCollector
    {
        private const Resource LeisureResources = Resource.Meals | Resource.Entertainment | Resource.Recreation | Resource.Lodging;
        private const Resource OfficeResources = Resource.Software | Resource.Telecom | Resource.Financial | Resource.Media;

        private readonly Func<object?> _getEntityManager;
        private readonly Func<Entity, string?>? _nameResolver;
        private readonly int _topProviderLimit;

        public LiveCommuteDestinationsCollector(
            Func<object?>? getEntityManager = null,
            Func<Entity, string?>? nameResolver = null,
            int topProviderLimit = 200)
        {
            _getEntityManager = getEntityManager ?? ResolveDefaultEntityManager;
            _nameResolver = nameResolver;
            _topProviderLimit = Math.Max(1, topProviderLimit);
        }

        public CommuteDestinationsCollectionResult Collect()
        {
            object? entityManagerOrNull = _getEntityManager();
            if (entityManagerOrNull == null)
            {
                return CommuteDestinationsCollectionResult.CreateFailure("Live commute destinations collector could not resolve a created entity manager.");
            }

            return CollectWithEntityManager(entityManagerOrNull);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private CommuteDestinationsCollectionResult CollectWithEntityManager(object entityManagerOrNull)
        {
            try
            {
                var entityManager = (EntityManager)entityManagerOrNull;
                List<ProviderSnapshot> providers = CollectProviderSnapshots(entityManager);
                CommuteDestinationsCollectionResult result = new CommuteDestinationsCollector(providers, _topProviderLimit).Collect();
                if (result.IsSuccess && result.Value != null)
                {
                    result.Value.Notes = (string[])CommuteDestinationConstants.DefaultNotes.Clone();
                }

                return result;
            }
            catch (Exception ex)
            {
                return CommuteDestinationsCollectionResult.CreateFailure(
                    "Live commute destinations collector failed: " + ex.Message);
            }
        }

        private List<ProviderSnapshot> CollectProviderSnapshots(EntityManager entityManager)
        {
            var providers = new List<ProviderSnapshot>();
            NameSystem? nameSystem = _nameResolver == null ? TryGetNameSystem(entityManager) : null;

            using EntityQuery query = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Employee>(),
                        ComponentType.ReadOnly<WorkProvider>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    },
                    Any = new[]
                    {
                        ComponentType.ReadOnly<PropertyRenter>(),
                        ComponentType.ReadOnly<Building>()
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>()
                    }
                });

            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.TempJob);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity providerEntity = entities[i];
                PrefabRef prefabRef = entityManager.GetComponentData<PrefabRef>(providerEntity);
                Entity providerPrefab = prefabRef.m_Prefab;
                if (providerPrefab == Entity.Null || !entityManager.HasComponent<WorkplaceData>(providerPrefab))
                {
                    continue;
                }

                WorkProvider workProvider = entityManager.GetComponentData<WorkProvider>(providerEntity);
                WorkplaceData workplaceData = entityManager.GetComponentData<WorkplaceData>(providerPrefab);
                DynamicBuffer<Employee> employees = entityManager.GetBuffer<Employee>(providerEntity);

                Entity? buildingEntity = ResolveBuildingEntity(entityManager, providerEntity);
                int buildingLevel = ResolveBuildingLevel(entityManager, buildingEntity);
                EmploymentData workplacesData = EmploymentData.GetWorkplacesData(
                    workProvider.m_MaxWorkers,
                    buildingLevel,
                    workplaceData.m_Complexity);

                int commuterEmployees = CountCommuterEmployees(entityManager, employees);
                int localEmployees = Math.Max(0, employees.Length - commuterEmployees);
                string sector = ClassifySector(entityManager, providerEntity, providerPrefab);
                Entity? districtEntity = ResolveDistrictEntity(entityManager, providerEntity, buildingEntity);

                providers.Add(
                    new ProviderSnapshot(
                        providerEntity: providerEntity.Index,
                        buildingEntity: ToNullableIndex(buildingEntity),
                        districtEntity: ToNullableIndex(districtEntity),
                        districtName: ResolveName(districtEntity, entityManager, nameSystem),
                        buildingName: ResolveName(buildingEntity ?? providerEntity, entityManager, nameSystem),
                        sector: sector,
                        jobsTotal: workplacesData.total,
                        jobsFilled: employees.Length,
                        commuterEmployees: commuterEmployees,
                        localEmployees: localEmployees));
            }

            return providers;
        }

        private string? ResolveName(Entity entity, EntityManager entityManager, NameSystem? nameSystem)
        {
            return ResolveName((Entity?)entity, entityManager, nameSystem);
        }

        private string? ResolveName(Entity? entity, EntityManager entityManager, NameSystem? nameSystem)
        {
            if (!entity.HasValue || entity.Value == Entity.Null)
            {
                return null;
            }

            if (_nameResolver != null)
            {
                return NormalizeName(_nameResolver(entity.Value));
            }

            if (nameSystem == null)
            {
                return null;
            }

            try
            {
                object rawName = nameSystem.GetName(entity.Value, false);
                if (TryExtractDisplayName(rawName, out string displayName))
                {
                    return displayName;
                }
            }
            catch
            {
                // Best effort only.
            }

            try
            {
                object rawName = nameSystem.GetNameForVirtualKeyboard(entity.Value);
                if (TryExtractDisplayName(rawName, out string displayName))
                {
                    return displayName;
                }
            }
            catch
            {
                // Best effort only.
            }

            return null;
        }

        private static object? ResolveDefaultEntityManager()
        {
            World? world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return null;
            }

            return world.EntityManager;
        }

        private static NameSystem? TryGetNameSystem(EntityManager entityManager)
        {
            try
            {
                return entityManager.World.GetExistingSystemManaged(typeof(NameSystem)) as NameSystem;
            }
            catch
            {
                return null;
            }
        }

        private static Entity? ResolveBuildingEntity(EntityManager entityManager, Entity providerEntity)
        {
            if (entityManager.HasComponent<PropertyRenter>(providerEntity))
            {
                Entity propertyEntity = entityManager.GetComponentData<PropertyRenter>(providerEntity).m_Property;
                if (propertyEntity != Entity.Null)
                {
                    return propertyEntity;
                }
            }

            if (entityManager.HasComponent<Building>(providerEntity))
            {
                return providerEntity;
            }

            return null;
        }

        private static int ResolveBuildingLevel(EntityManager entityManager, Entity? buildingEntity)
        {
            if (!buildingEntity.HasValue || buildingEntity.Value == Entity.Null)
            {
                return 1;
            }

            Entity buildingValue = buildingEntity.Value;
            if (!entityManager.HasComponent<PrefabRef>(buildingValue))
            {
                return 1;
            }

            Entity buildingPrefab = entityManager.GetComponentData<PrefabRef>(buildingValue).m_Prefab;
            if (buildingPrefab == Entity.Null || !entityManager.HasComponent<SpawnableBuildingData>(buildingPrefab))
            {
                return 1;
            }

            return (int)entityManager.GetComponentData<SpawnableBuildingData>(buildingPrefab).m_Level;
        }

        private static Entity? ResolveDistrictEntity(EntityManager entityManager, Entity providerEntity, Entity? buildingEntity)
        {
            Entity? district = TryResolveDistrict(entityManager, buildingEntity);
            if (district.HasValue)
            {
                return district;
            }

            return TryResolveDistrict(entityManager, providerEntity);
        }

        private static Entity? TryResolveDistrict(EntityManager entityManager, Entity? entity)
        {
            if (!entity.HasValue || entity.Value == Entity.Null || !entityManager.HasComponent<CurrentDistrict>(entity.Value))
            {
                return null;
            }

            Entity districtEntity = entityManager.GetComponentData<CurrentDistrict>(entity.Value).m_District;
            return districtEntity == Entity.Null ? null : districtEntity;
        }

        private static int? ToNullableIndex(Entity? entity)
        {
            return entity.HasValue && entity.Value != Entity.Null
                ? entity.Value.Index
                : (int?)null;
        }

        private static int CountCommuterEmployees(EntityManager entityManager, DynamicBuffer<Employee> employees)
        {
            int commuters = 0;
            for (int i = 0; i < employees.Length; i++)
            {
                Entity workerEntity = employees[i].m_Worker;
                if (!entityManager.HasComponent<Citizen>(workerEntity))
                {
                    continue;
                }

                Citizen citizen = entityManager.GetComponentData<Citizen>(workerEntity);
                if ((citizen.m_State & CitizenFlags.Commuter) != 0)
                {
                    commuters++;
                }
            }

            return commuters;
        }

        private static string ClassifySector(EntityManager entityManager, Entity providerEntity, Entity providerPrefab)
        {
            bool isExtractor = entityManager.HasComponent<Game.Companies.ExtractorCompany>(providerEntity);
            bool isIndustrial = entityManager.HasComponent<IndustrialCompany>(providerEntity);
            bool isCommercial = entityManager.HasComponent<CommercialCompany>(providerEntity);
            bool isService = !isIndustrial && !isCommercial;

            bool isOffice = false;
            bool isLeisure = false;
            if (entityManager.HasComponent<IndustrialProcessData>(providerPrefab))
            {
                IndustrialProcessData process = entityManager.GetComponentData<IndustrialProcessData>(providerPrefab);
                Resource output = process.m_Output.m_Resource;
                isLeisure = (output & LeisureResources) != Resource.NoResource;
                isOffice = (output & OfficeResources) != Resource.NoResource;
            }

            if (isService)
            {
                return "service";
            }

            if (isCommercial)
            {
                return isLeisure ? "leisure" : "commercial";
            }

            if (isExtractor)
            {
                return "extractor";
            }

            return isOffice ? "office" : "industrial";
        }

        private static bool TryExtractDisplayName(object? rawName, out string displayName)
        {
            displayName = string.Empty;
            if (TryNormalizeName(rawName, out displayName))
            {
                return true;
            }

            if (rawName == null)
            {
                return false;
            }

            Type rawType = rawName.GetType();
            foreach (string memberName in new[] { "name", "Name", "displayName", "DisplayName", "text", "Text", "value", "Value" })
            {
                PropertyInfo? property = rawType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead && TryNormalizeName(property.GetValue(rawName), out displayName))
                {
                    return true;
                }

                FieldInfo? field = rawType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && TryNormalizeName(field.GetValue(rawName), out displayName))
                {
                    return true;
                }
            }

            if (rawName is IEnumerable enumerable && rawName is not string)
            {
                foreach (object? entry in enumerable)
                {
                    if (TryNormalizeName(entry, out displayName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string? NormalizeName(string? value)
        {
            return TryNormalizeName(value, out string displayName)
                ? displayName
                : null;
        }

        private static bool TryNormalizeName(object? rawName, out string displayName)
        {
            displayName = string.Empty;
            if (rawName == null)
            {
                return false;
            }

            string candidate = rawName.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate) ||
                string.Equals(candidate, "null", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            displayName = candidate;
            return true;
        }
    }
}
