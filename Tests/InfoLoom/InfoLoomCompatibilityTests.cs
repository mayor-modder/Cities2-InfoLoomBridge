using System;
using System.IO;
using System.Reflection;
using InfoLoomBridge.InfoLoom;
using Xunit;

namespace InfoLoomBridge.Tests.InfoLoom
{
    public sealed class InfoLoomCompatibilityTests
    {
        [Fact]
        public void Detect_reports_missing_when_InfoLoom_is_not_available()
        {
            var report = InfoLoomCompatibility.Detect(new StubCompatibilitySource(false, null));

            Assert.Equal(InfoLoomCompatibilityState.Missing, report.State);
            Assert.False(report.IsInstalled);
            Assert.Null(report.DetectedVersion);
        }

        [Fact]
        public void Detect_reports_supported_when_InfoLoom_version_is_supported()
        {
            var report = InfoLoomCompatibility.Detect(new StubCompatibilitySource(true, new Version(1, 2, 3)));

            Assert.Equal(InfoLoomCompatibilityState.Supported, report.State);
            Assert.True(report.IsInstalled);
            Assert.Equal(new Version(1, 2, 3), report.DetectedVersion);
            Assert.NotNull(report.BuildFingerprint);
            Assert.Equal("1.2.3+abcdef", report.BuildFingerprint!.ProductVersion);
            Assert.Equal("abcdef", report.BuildFingerprint.GitCommit);
        }

        [Fact]
        public void Detect_reports_incompatible_when_InfoLoom_version_is_unsupported()
        {
            var report = InfoLoomCompatibility.Detect(new StubCompatibilitySource(true, new Version(2, 0, 0)));

            Assert.Equal(InfoLoomCompatibilityState.Incompatible, report.State);
            Assert.True(report.IsInstalled);
            Assert.Equal(new Version(2, 0, 0), report.DetectedVersion);
        }

        [Fact]
        public void Detect_reports_supported_when_InfoLoomTwo_dll_is_found_in_a_subscribed_mod_cache_layout()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                string subscribedRoot = Path.Combine(tempRoot, "mods_subscribed", "91433_36");
                Directory.CreateDirectory(subscribedRoot);

                string probeAssembly = typeof(InfoLoomCompatibilityTests).Assembly.Location;
                string candidatePath = Path.Combine(subscribedRoot, "InfoLoomTwo.dll");
                File.Copy(probeAssembly, candidatePath);

                IInfoLoomCompatibilitySource source = CreateFileSystemCompatibilitySource(tempRoot);

                InfoLoomCompatibilityReport report = InfoLoomCompatibility.Detect(source);

                Assert.Equal(InfoLoomCompatibilityState.Supported, report.State);
                Assert.True(report.IsInstalled);
                Assert.True(report.IsLoadable);
                Assert.Equal(candidatePath, report.Location);
                Assert.NotNull(report.DetectedVersion);
                Assert.NotNull(report.BuildFingerprint);
                Assert.Equal(candidatePath, report.BuildFingerprint!.Location);
                Assert.Equal(report.DetectedVersion, report.BuildFingerprint.AssemblyVersion);
                Assert.NotNull(report.BuildFingerprint.FileLastWriteUtc);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void Detect_reports_supported_when_InfoLoomTwo_dll_is_found_in_the_pdx_mods_cache_layout()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                string subscribedRoot = Path.Combine(tempRoot, "pdx_mods", "91433_38");
                Directory.CreateDirectory(subscribedRoot);

                string probeAssembly = typeof(InfoLoomCompatibilityTests).Assembly.Location;
                string candidatePath = Path.Combine(subscribedRoot, "InfoLoomTwo.dll");
                File.Copy(probeAssembly, candidatePath);

                IInfoLoomCompatibilitySource source = CreateFileSystemCompatibilitySource(tempRoot);

                InfoLoomCompatibilityReport report = InfoLoomCompatibility.Detect(source);

                Assert.Equal(InfoLoomCompatibilityState.Supported, report.State);
                Assert.True(report.IsInstalled);
                Assert.True(report.IsLoadable);
                Assert.Equal(candidatePath, report.Location);
                Assert.NotNull(report.DetectedVersion);
                Assert.NotNull(report.BuildFingerprint);
                Assert.Equal(candidatePath, report.BuildFingerprint!.Location);
                Assert.Equal(report.DetectedVersion, report.BuildFingerprint.AssemblyVersion);
                Assert.NotNull(report.BuildFingerprint.FileLastWriteUtc);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private sealed class StubCompatibilitySource : IInfoLoomCompatibilitySource
        {
            private readonly bool _isInstalled;
            private readonly Version? _version;

            public StubCompatibilitySource(bool isInstalled, Version? version)
            {
                _isInstalled = isInstalled;
                _version = version;
            }

            public bool TryGetInfoLoom(out InfoLoomDependencyInfo dependencyInfo)
            {
                if (_isInstalled)
                {
                    dependencyInfo = new InfoLoomDependencyInfo(
                        true,
                        _version,
                        @"C:\Mods\InfoLoom",
                        new InfoLoomBuildFingerprint(
                            @"C:\Mods\InfoLoom",
                            _version,
                            "1.2.3+abcdef",
                            "abcdef",
                            DateTimeOffset.Parse("2026-04-04T12:00:00Z")));
                    return true;
                }

                dependencyInfo = default;
                return false;
            }
        }

        private static IInfoLoomCompatibilitySource CreateFileSystemCompatibilitySource(string modsRoot)
        {
            Type? sourceType = typeof(InfoLoomCompatibility).GetNestedType(
                "FileSystemInfoLoomCompatibilitySource",
                BindingFlags.NonPublic);

            Assert.NotNull(sourceType);

            object? instance = Activator.CreateInstance(
                sourceType!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { modsRoot },
                culture: null);

            return Assert.IsAssignableFrom<IInfoLoomCompatibilitySource>(instance);
        }
    }
}
