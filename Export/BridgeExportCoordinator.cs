using System;
using System.IO;
using InfoLoomBridge.InfoLoom;
using InfoLoomBridge.Runtime;

namespace InfoLoomBridge.Export
{
    internal interface IBridgeExportCoordinator : IDisposable
    {
        void PublishIfDue(DateTimeOffset nowUtc);
    }

    public sealed class BridgeExportCoordinator : IDisposable, IBridgeExportCoordinator
    {
        private static readonly TimeSpan DefaultPublishInterval = TimeSpan.FromSeconds(30);

        private readonly IInfoLoomAdapter _adapter;
        private readonly Func<InfoLoomCompatibilityReport> _compatibilityProvider;
        private readonly ICommuteDestinationsCollector _commuteCollector;
        private readonly Action<string> _logger;
        private readonly TimeSpan _publishInterval;
        private readonly string _outputRoot;
        private DateTimeOffset _nextDueUtc;

        public BridgeExportCoordinator(
            IInfoLoomAdapter adapter,
            Func<InfoLoomCompatibilityReport> compatibilityProvider,
            ICommuteDestinationsCollector commuteCollector,
            Action<string> logger,
            string? outputRootOverride = null,
            TimeSpan? publishInterval = null)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _compatibilityProvider = compatibilityProvider ?? throw new ArgumentNullException(nameof(compatibilityProvider));
            _commuteCollector = commuteCollector ?? throw new ArgumentNullException(nameof(commuteCollector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _publishInterval = publishInterval ?? DefaultPublishInterval;
            _outputRoot = string.IsNullOrWhiteSpace(outputRootOverride)
                ? ResolveOutputRoot()
                : Path.GetFullPath(outputRootOverride);
            _nextDueUtc = DateTimeOffset.MinValue;
        }

        public string OutputRoot => _outputRoot;

        public string LatestSnapshotPath => Path.Combine(_outputRoot, "latest.json");

        public void Dispose()
        {
        }

        public void PublishIfDue(DateTimeOffset nowUtc)
        {
            if (nowUtc < _nextDueUtc)
            {
                return;
            }

            try
            {
                BridgeSnapshot snapshot = CreateSnapshot(nowUtc);
                WriteLatestJsonAtomic(snapshot);
                if (snapshot.Status == "error" && !string.IsNullOrWhiteSpace(snapshot.Message))
                {
                    _logger(snapshot.Message!);
                }
            }
            catch (Exception ex)
            {
                string message = $"InfoLoom bridge export failed: {ex.Message}";
                _logger(message);
                TryWriteUnexpectedFailureSnapshot(nowUtc, message);
            }
            finally
            {
                _nextDueUtc = nowUtc + _publishInterval;
            }
        }

        internal BridgeSnapshot CreateSnapshot(DateTimeOffset nowUtc)
        {
            InfoLoomCompatibilityReport compatibility = _compatibilityProvider();
            var snapshot = new BridgeSnapshot
            {
                ExportVersion = "1.0.0",
                GeneratedAt = nowUtc,
                BridgeVersion = GetBridgeVersion(),
                InfoLoomVersion = compatibility.DetectedVersion?.ToString() ?? string.Empty,
                InfoLoomBuild = compatibility.BuildFingerprint,
                Status = "error",
                BridgeExtensions = CreateBridgeExtensions()
            };

            if (compatibility.State != InfoLoomCompatibilityState.Supported)
            {
                snapshot.Message = compatibility.LogMessage;
                return snapshot;
            }

            InfoLoomFirstPanelSliceResult panelSlice = _adapter.ReadFirstPanelSlice();
            if (!panelSlice.IsSuccess || panelSlice.Panels == null)
            {
                snapshot.Message = FormatFailure(panelSlice.Failure);
                return snapshot;
            }

            snapshot.Status = "ok";
            snapshot.Message = null;
            snapshot.Panels = new BridgePanelsSnapshot
            {
                Demographics = panelSlice.Panels.Demographics,
                Workforce = panelSlice.Panels.Workforce,
                Workplaces = panelSlice.Panels.Workplaces
            };

            return snapshot;
        }

        private BridgeExtensionsSnapshot CreateBridgeExtensions()
        {
            CommuteDestinationsCollectionResult commuteResult = _commuteCollector.Collect();
            if (!commuteResult.IsSuccess || commuteResult.Value == null)
            {
                return new BridgeExtensionsSnapshot
                {
                    CommuteDestinations = new CommuteDestinationsExtensionSnapshot
                    {
                        Status = "error",
                        Message = commuteResult.ErrorMessage ?? "Commute destination scan failed.",
                        SourceComponent = CommuteDestinationConstants.SourceComponent,
                        Notes = (string[])CommuteDestinationConstants.DefaultNotes.Clone()
                    }
                };
            }

            CommuteDestinationsCollectedData value = commuteResult.Value;
            return new BridgeExtensionsSnapshot
            {
                CommuteDestinations = new CommuteDestinationsExtensionSnapshot
                {
                    Status = "ok",
                    SourceComponent = CommuteDestinationConstants.SourceComponent,
                    Notes = value.Notes ?? (string[])CommuteDestinationConstants.DefaultNotes.Clone(),
                    ByDistrict = value.ByDistrict ?? Array.Empty<CommuteDestinationDistrictRow>(),
                    TopWorkProviders = value.TopWorkProviders ?? Array.Empty<CommuteDestinationProviderRow>(),
                    ProviderRowsTotal = value.ProviderRowsTotal,
                    ProviderRowsExported = value.ProviderRowsExported,
                    ProviderRowsTruncated = value.ProviderRowsTruncated
                }
            };
        }

        internal void WriteLatestJsonAtomic(BridgeSnapshot snapshot)
        {
            string latestPath = LatestSnapshotPath;
            string tempPath = latestPath + ".tmp";

            Directory.CreateDirectory(_outputRoot);
            File.WriteAllText(tempPath, BridgeExportWriter.Serialize(snapshot));

            try
            {
                if (File.Exists(latestPath))
                {
                    File.Replace(tempPath, latestPath, null);
                }
                else
                {
                    File.Move(tempPath, latestPath);
                }
            }
            catch
            {
                TryDeleteTempFile(tempPath);
                throw;
            }
        }

        internal static string ResolveOutputRoot()
        {
            return ResolveOutputRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        internal static string ResolveOutputRoot(string localApplicationDataPath)
        {
            if (string.IsNullOrWhiteSpace(localApplicationDataPath))
            {
                throw new ArgumentException("Local application data path must be provided.", nameof(localApplicationDataPath));
            }

            string localLow = Path.GetFullPath(Path.Combine(localApplicationDataPath, "..", "LocalLow"));
            return Path.Combine(
                localLow,
                "Colossal Order",
                "Cities Skylines II",
                "ModsData",
                "InfoLoomBridge");
        }

        private static string GetBridgeVersion()
        {
            return typeof(BridgeExportCoordinator).Assembly.GetName().Version?.ToString() ?? string.Empty;
        }

        private static string FormatFailure(InfoLoomCompatibilityFailure? failure)
        {
            if (failure == null)
            {
                return "InfoLoom adapter failed without a structured error.";
            }

            if (string.IsNullOrWhiteSpace(failure.Detail))
            {
                return failure.Message;
            }

            return $"{failure.Message}: {failure.Detail}";
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Non-fatal cleanup best effort.
            }
        }

        private void TryWriteUnexpectedFailureSnapshot(DateTimeOffset nowUtc, string message)
        {
            try
            {
                WriteLatestJsonAtomic(
                    new BridgeSnapshot
                    {
                        ExportVersion = "1.0.0",
                        GeneratedAt = nowUtc,
                        BridgeVersion = GetBridgeVersion(),
                        InfoLoomVersion = string.Empty,
                        Status = "error",
                        Message = message
                    });
            }
            catch (Exception ex)
            {
                _logger($"InfoLoom bridge export failed to write error snapshot: {ex.Message}");
            }
        }
    }
}
