using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;

namespace InfoLoomBridge.Export
{
    public static class BridgeExportWriter
    {
        public static string Serialize(BridgeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendProperty(builder, "export_version", snapshot.ExportVersion);
            builder.Append(',');
            AppendProperty(builder, "generated_at", snapshot.GeneratedAt);
            builder.Append(',');
            AppendProperty(builder, "bridge_version", snapshot.BridgeVersion);
            builder.Append(',');
            AppendProperty(builder, "infoloom_version", snapshot.InfoLoomVersion);
            if (snapshot.InfoLoomBuild != null)
            {
                builder.Append(',');
                AppendProperty(builder, "infoloom_build", snapshot.InfoLoomBuild);
            }
            builder.Append(',');
            AppendProperty(builder, "status", snapshot.Status);

            if (snapshot.Message != null)
            {
                builder.Append(',');
                AppendProperty(builder, "message", snapshot.Message);
            }

            builder.Append(',');
            builder.Append("\"panels\":");
            AppendPanels(builder, snapshot.Panels);
            builder.Append(',');
            builder.Append("\"bridge_extensions\":");
            AppendBridgeExtensions(builder, snapshot.BridgeExtensions);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendBridgeExtensions(StringBuilder builder, BridgeExtensionsSnapshot? extensions)
        {
            builder.Append('{');
            AppendProperty(builder, "commute_destinations", extensions?.CommuteDestinations);
            builder.Append('}');
        }

        private static void AppendBuildFingerprint(StringBuilder builder, InfoLoomBuildFingerprint value)
        {
            builder.Append('{');
            AppendProperty(builder, "location", value.Location);
            builder.Append(',');
            AppendProperty(builder, "assembly_version", value.AssemblyVersion?.ToString());
            builder.Append(',');
            AppendProperty(builder, "product_version", value.ProductVersion);
            builder.Append(',');
            AppendProperty(builder, "git_commit", value.GitCommit);
            builder.Append(',');
            AppendProperty(builder, "file_last_write_utc", value.FileLastWriteUtc);
            builder.Append('}');
        }

        private static void AppendPanels(StringBuilder builder, BridgePanelsSnapshot? panels)
        {
            builder.Append('{');
            AppendProperty(builder, "demographics", panels?.Demographics);
            builder.Append(',');
            AppendProperty(builder, "workforce", panels?.Workforce);
            builder.Append(',');
            AppendProperty(builder, "workplaces", panels?.Workplaces);
            builder.Append('}');
        }

        private static void AppendProperty(StringBuilder builder, string name, object? value)
        {
            AppendEscapedString(builder, name);
            builder.Append(':');
            AppendValue(builder, value);
        }

        private static void AppendValue(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case string text:
                    AppendEscapedString(builder, text);
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
                case DateTimeOffset dateTimeOffset:
                    AppendEscapedString(builder, dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                    return;
                case Enum enumValue:
                    AppendEscapedString(builder, enumValue.ToString());
                    return;
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case IDictionary<string, object?> mutableDictionary:
                    AppendDictionary(builder, mutableDictionary);
                    return;
                case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                    AppendDictionary(builder, readOnlyDictionary);
                    return;
                case InfoLoomBuildFingerprint buildFingerprint:
                    AppendBuildFingerprint(builder, buildFingerprint);
                    return;
                case BridgeExtensionsSnapshot extensions:
                    AppendBridgeExtensions(builder, extensions);
                    return;
                case CommuteDestinationsExtensionSnapshot commuteDestinations:
                    AppendCommuteDestinations(builder, commuteDestinations);
                    return;
                case CommuteDestinationDistrictRow districtRow:
                    AppendDistrictRow(builder, districtRow);
                    return;
                case CommuteDestinationProviderRow providerRow:
                    AppendProviderRow(builder, providerRow);
                    return;
                case SectorIntValueSet sectorValues:
                    AppendSectorValues(builder, sectorValues);
                    return;
                case IEnumerable enumerable when value is not string:
                    AppendEnumerable(builder, enumerable);
                    return;
                default:
                    AppendObject(builder, value);
                    return;
            }
        }

        private static void AppendDictionary(StringBuilder builder, IEnumerable<KeyValuePair<string, object?>> dictionary)
        {
            builder.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object?> entry in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                AppendProperty(builder, entry.Key, entry.Value);
                first = false;
            }

            builder.Append('}');
        }

        private static void AppendCommuteDestinations(StringBuilder builder, CommuteDestinationsExtensionSnapshot value)
        {
            builder.Append('{');
            AppendProperty(builder, "status", value.Status);
            if (value.Message != null)
            {
                builder.Append(',');
                AppendProperty(builder, "message", value.Message);
            }

            builder.Append(',');
            AppendProperty(builder, "source_component", value.SourceComponent);
            builder.Append(',');
            AppendProperty(builder, "notes", value.Notes);
            builder.Append(',');
            AppendProperty(builder, "by_district", value.ByDistrict);
            builder.Append(',');
            AppendProperty(builder, "top_work_providers", value.TopWorkProviders);
            builder.Append(',');
            AppendProperty(builder, "provider_rows_total", value.ProviderRowsTotal);
            builder.Append(',');
            AppendProperty(builder, "provider_rows_exported", value.ProviderRowsExported);
            builder.Append(',');
            AppendProperty(builder, "provider_rows_truncated", value.ProviderRowsTruncated);
            builder.Append('}');
        }

        private static void AppendDistrictRow(StringBuilder builder, CommuteDestinationDistrictRow value)
        {
            builder.Append('{');
            AppendProperty(builder, "district_entity", value.DistrictEntity);
            builder.Append(',');
            AppendProperty(builder, "district_name", value.DistrictName);
            builder.Append(',');
            AppendProperty(builder, "provider_count", value.ProviderCount);
            builder.Append(',');
            AppendProperty(builder, "jobs_total", value.JobsTotal);
            builder.Append(',');
            AppendProperty(builder, "jobs_filled", value.JobsFilled);
            builder.Append(',');
            AppendProperty(builder, "jobs_open", value.JobsOpen);
            builder.Append(',');
            AppendProperty(builder, "commuter_employees", value.CommuterEmployees);
            builder.Append(',');
            AppendProperty(builder, "local_employees", value.LocalEmployees);
            builder.Append(',');
            AppendProperty(builder, "sector_commuter_employees", value.SectorCommuterEmployees);
            builder.Append('}');
        }

        private static void AppendProviderRow(StringBuilder builder, CommuteDestinationProviderRow value)
        {
            builder.Append('{');
            AppendProperty(builder, "provider_entity", value.ProviderEntity);
            builder.Append(',');
            AppendProperty(builder, "building_entity", value.BuildingEntity);
            builder.Append(',');
            AppendProperty(builder, "district_entity", value.DistrictEntity);
            builder.Append(',');
            AppendProperty(builder, "district_name", value.DistrictName);
            builder.Append(',');
            AppendProperty(builder, "building_name", value.BuildingName);
            builder.Append(',');
            AppendProperty(builder, "company_name", value.CompanyName);
            builder.Append(',');
            AppendProperty(builder, "sector", value.Sector);
            builder.Append(',');
            AppendProperty(builder, "jobs_total", value.JobsTotal);
            builder.Append(',');
            AppendProperty(builder, "jobs_filled", value.JobsFilled);
            builder.Append(',');
            AppendProperty(builder, "jobs_open", value.JobsOpen);
            builder.Append(',');
            AppendProperty(builder, "commuter_employees", value.CommuterEmployees);
            builder.Append(',');
            AppendProperty(builder, "local_employees", value.LocalEmployees);
            builder.Append('}');
        }

        private static void AppendSectorValues(StringBuilder builder, SectorIntValueSet value)
        {
            builder.Append('{');
            AppendProperty(builder, "service", value.Service);
            builder.Append(',');
            AppendProperty(builder, "commercial", value.Commercial);
            builder.Append(',');
            AppendProperty(builder, "leisure", value.Leisure);
            builder.Append(',');
            AppendProperty(builder, "extractor", value.Extractor);
            builder.Append(',');
            AppendProperty(builder, "industrial", value.Industrial);
            builder.Append(',');
            AppendProperty(builder, "office", value.Office);
            builder.Append('}');
        }

        private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable)
        {
            builder.Append('[');
            bool first = true;
            foreach (object? item in enumerable)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                AppendValue(builder, item);
                first = false;
            }

            builder.Append(']');
        }

        private static void AppendObject(StringBuilder builder, object value)
        {
            PropertyInfo[] properties = value
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public);

            builder.Append('{');
            bool first = true;
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                AppendProperty(builder, property.Name, property.GetValue(value, null));
                first = false;
            }

            builder.Append('}');
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
