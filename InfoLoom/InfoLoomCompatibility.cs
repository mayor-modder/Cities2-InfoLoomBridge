using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace InfoLoomBridge.InfoLoom
{
    public enum InfoLoomCompatibilityState
    {
        Missing = 0,
        Supported = 1,
        Incompatible = 2
    }

    public sealed class InfoLoomBuildFingerprint
    {
        public InfoLoomBuildFingerprint(
            string? location,
            Version? assemblyVersion,
            string? productVersion,
            string? gitCommit,
            DateTimeOffset? fileLastWriteUtc)
        {
            Location = location;
            AssemblyVersion = assemblyVersion;
            ProductVersion = productVersion;
            GitCommit = gitCommit;
            FileLastWriteUtc = fileLastWriteUtc;
        }

        public string? Location { get; }

        public Version? AssemblyVersion { get; }

        public string? ProductVersion { get; }

        public string? GitCommit { get; }

        public DateTimeOffset? FileLastWriteUtc { get; }

        public static string? ParseGitCommit(string? productVersion)
        {
            if (string.IsNullOrWhiteSpace(productVersion))
            {
                return null;
            }

            string productVersionValue = productVersion!;
            int separatorIndex = productVersionValue.IndexOf('+');
            if (separatorIndex < 0 || separatorIndex == productVersionValue.Length - 1)
            {
                return null;
            }

            string candidate = productVersionValue.Substring(separatorIndex + 1).Trim();
            if (candidate.Length < 7)
            {
                return null;
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                if (!Uri.IsHexDigit(candidate[i]))
                {
                    return null;
                }
            }

            return candidate;
        }
    }

    public readonly struct InfoLoomDependencyInfo
    {
        public InfoLoomDependencyInfo(bool isLoadable, Version? version, string? location, InfoLoomBuildFingerprint? buildFingerprint = null)
        {
            IsLoadable = isLoadable;
            Version = version;
            Location = location;
            BuildFingerprint = buildFingerprint;
        }

        public bool IsLoadable { get; }

        public Version? Version { get; }

        public string? Location { get; }

        public InfoLoomBuildFingerprint? BuildFingerprint { get; }
    }

    public interface IInfoLoomCompatibilitySource
    {
        bool TryGetInfoLoom(out InfoLoomDependencyInfo dependencyInfo);
    }

    public sealed class InfoLoomCompatibilityReport
    {
        private InfoLoomCompatibilityReport(
            InfoLoomCompatibilityState state,
            bool isInstalled,
            bool isLoadable,
            Version? detectedVersion,
            string? location,
            InfoLoomBuildFingerprint? buildFingerprint)
        {
            State = state;
            IsInstalled = isInstalled;
            IsLoadable = isLoadable;
            DetectedVersion = detectedVersion;
            Location = location;
            BuildFingerprint = buildFingerprint;
        }

        public InfoLoomCompatibilityState State { get; }

        public bool IsInstalled { get; }

        public bool IsLoadable { get; }

        public Version? DetectedVersion { get; }

        public string? Location { get; }

        public InfoLoomBuildFingerprint? BuildFingerprint { get; }

        public string StatusText => State switch
        {
            InfoLoomCompatibilityState.Supported => "supported",
            InfoLoomCompatibilityState.Incompatible => "incompatible",
            _ => "missing"
        };

        public string LogMessage
        {
            get
            {
                if (!IsInstalled)
                {
                    return "InfoLoom dependency missing";
                }

                string versionText = DetectedVersion?.ToString() ?? "unknown";
                return State == InfoLoomCompatibilityState.Supported
                    ? $"InfoLoom dependency supported ({versionText})"
                    : $"InfoLoom dependency incompatible ({versionText})";
            }
        }

        public static InfoLoomCompatibilityReport Missing()
        {
            return new InfoLoomCompatibilityReport(
                InfoLoomCompatibilityState.Missing,
                isInstalled: false,
                isLoadable: false,
                detectedVersion: null,
                location: null,
                buildFingerprint: null);
        }

        public static InfoLoomCompatibilityReport Supported(InfoLoomDependencyInfo dependencyInfo)
        {
            return new InfoLoomCompatibilityReport(
                InfoLoomCompatibilityState.Supported,
                isInstalled: true,
                isLoadable: dependencyInfo.IsLoadable,
                detectedVersion: dependencyInfo.Version,
                location: dependencyInfo.Location,
                buildFingerprint: dependencyInfo.BuildFingerprint);
        }

        public static InfoLoomCompatibilityReport Incompatible(InfoLoomDependencyInfo dependencyInfo)
        {
            return new InfoLoomCompatibilityReport(
                InfoLoomCompatibilityState.Incompatible,
                isInstalled: true,
                isLoadable: dependencyInfo.IsLoadable,
                detectedVersion: dependencyInfo.Version,
                location: dependencyInfo.Location,
                buildFingerprint: dependencyInfo.BuildFingerprint);
        }
    }

    public static class InfoLoomCompatibility
    {
        private static readonly Version MinimumSupportedVersion = new Version(1, 0, 0);
        private static readonly Version MaximumSupportedVersion = new Version(2, 0, 0);
        private static readonly string[] SupportedAssemblyFileNames = { "InfoLoom.dll", "InfoLoomTwo.dll" };

        public static InfoLoomCompatibilityReport Detect()
        {
            return Detect(new CompositeInfoLoomCompatibilitySource(
                new FileSystemInfoLoomCompatibilitySource(GetDefaultModsRoot()),
                new FileSystemInfoLoomCompatibilitySource(GetDefaultSubscribedModsRoot()),
                new FileSystemInfoLoomCompatibilitySource(GetDefaultPdxModsRoot())));
        }

        public static InfoLoomCompatibilityReport Detect(IInfoLoomCompatibilitySource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.TryGetInfoLoom(out InfoLoomDependencyInfo dependencyInfo))
            {
                return InfoLoomCompatibilityReport.Missing();
            }

            if (dependencyInfo.Version != null && IsSupportedVersion(dependencyInfo.Version))
            {
                return InfoLoomCompatibilityReport.Supported(dependencyInfo);
            }

            return InfoLoomCompatibilityReport.Incompatible(dependencyInfo);
        }

        private static bool IsSupportedVersion(Version version)
        {
            return version >= MinimumSupportedVersion && version < MaximumSupportedVersion;
        }

        private static string GetDefaultModsRoot()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "AppData", "LocalLow", "Colossal Order", "Cities Skylines II", "Mods");
        }

        private static string GetDefaultSubscribedModsRoot()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "AppData", "LocalLow", "Colossal Order", "Cities Skylines II", ".cache", "Mods", "mods_subscribed");
        }

        private static string GetDefaultPdxModsRoot()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "AppData", "LocalLow", "Colossal Order", "Cities Skylines II", ".cache", "Mods", "pdx_mods");
        }

        private sealed class CompositeInfoLoomCompatibilitySource : IInfoLoomCompatibilitySource
        {
            private readonly IInfoLoomCompatibilitySource[] _sources;

            public CompositeInfoLoomCompatibilitySource(params IInfoLoomCompatibilitySource[] sources)
            {
                _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            }

            public bool TryGetInfoLoom(out InfoLoomDependencyInfo dependencyInfo)
            {
                foreach (IInfoLoomCompatibilitySource source in _sources)
                {
                    if (source != null && source.TryGetInfoLoom(out dependencyInfo))
                    {
                        return true;
                    }
                }

                dependencyInfo = default;
                return false;
            }
        }

        private sealed class FileSystemInfoLoomCompatibilitySource : IInfoLoomCompatibilitySource
        {
            private readonly string _modsRoot;

            public FileSystemInfoLoomCompatibilitySource(string modsRoot)
            {
                _modsRoot = modsRoot;
            }

            public bool TryGetInfoLoom(out InfoLoomDependencyInfo dependencyInfo)
            {
                dependencyInfo = default;

                if (!Directory.Exists(_modsRoot))
                {
                    return false;
                }

                foreach (string assemblyFileName in SupportedAssemblyFileNames)
                {
                    string[] candidates = Directory.GetFiles(_modsRoot, assemblyFileName, SearchOption.AllDirectories);
                    foreach (string candidate in candidates)
                    {
                        Version? version = TryReadAssemblyVersion(candidate);
                        if (version == null)
                        {
                            continue;
                        }

                        string? productVersion = TryReadProductVersion(candidate);
                        dependencyInfo = new InfoLoomDependencyInfo(
                            true,
                            version,
                            candidate,
                            new InfoLoomBuildFingerprint(
                                candidate,
                                version,
                                productVersion,
                                InfoLoomBuildFingerprint.ParseGitCommit(productVersion),
                                TryReadFileLastWriteUtc(candidate)));
                        return true;
                    }
                }

                dependencyInfo = default;
                return false;
            }

            private static Version? TryReadAssemblyVersion(string path)
            {
                try
                {
                    return AssemblyName.GetAssemblyName(path).Version;
                }
                catch
                {
                    return null;
                }
            }

            private static string? TryReadProductVersion(string path)
            {
                try
                {
                    return FileVersionInfo.GetVersionInfo(path).ProductVersion;
                }
                catch
                {
                    return null;
                }
            }

            private static DateTimeOffset? TryReadFileLastWriteUtc(string path)
            {
                try
                {
                    DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                    if (lastWriteUtc == DateTime.MinValue)
                    {
                        return null;
                    }

                    return new DateTimeOffset(lastWriteUtc, TimeSpan.Zero);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
