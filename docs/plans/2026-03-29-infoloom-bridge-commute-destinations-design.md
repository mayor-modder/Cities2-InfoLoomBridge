# InfoLoom Bridge Commute Destinations Design

## Goal

Extend `InfoLoomBridge` so it can export live commuter-destination analysis that helps answer:

- which districts are importing the most workers
- which work providers are using the most commuters
- whether commuter pressure is concentrated in office, industrial, or other sectors

This slice is meant to explain the current high-unemployment / high-commuter city state more directly than the existing `workplaces` aggregate panel.

## Scope

- Keep the existing three InfoLoom-backed panels unchanged:
  - `demographics`
  - `workforce`
  - `workplaces`
- Add a new bridge-owned runtime export for commuter destinations.
- Attribute commuter-heavy jobs to destination districts and individual work providers.
- Reuse the bridge's normal publish cadence and atomic `latest.json` write path.
- Keep the export file local and file-backed for `Cities2-MCP`.

## Non-Goals

- No outside-connection origin attribution yet.
- No claim that the new commute data is an InfoLoom-owned panel.
- No in-mod localhost API or socket transport.
- No deep normalization into `Cities2-DataExport` schema terms inside the bridge.
- No requirement that provider or company names always resolve; stable entity ids are more important.

## Current Limitation

The current bridge export only mirrors the first validated InfoLoom panel slice:

```json
{
  "panels": {
    "demographics": { "...": "InfoLoom-backed payload" },
    "workforce": { "...": "InfoLoom-backed payload" },
    "workplaces": { "...": "InfoLoom-backed payload" }
  }
}
```

That is enough to show commuter pressure by workplace level, but not enough to answer:

- which district is the commuter sink
- which providers are pulling commuters
- whether one industrial park or cluster is dominating the mismatch

## Approach Options

### Option 1: Overload the existing `workplaces` panel

Append district/provider commuter breakdowns under `panels.workplaces`.

- Pros: fewer top-level schema changes
- Cons: dishonest source boundary, because the new data is not actually emitted by InfoLoom's workplaces panel

### Option 2: Add a bridge-owned extension section

Keep `panels.*` for InfoLoom-backed payloads and add a new bridge extension for commuter-destination analysis.

- Pros: honest provenance, easier to evolve, keeps InfoLoom compatibility logic separate from new runtime scans
- Cons: one more top-level schema concept for MCP to read

### Option 3: Keep the bridge unchanged and derive everything in MCP

Use only the current bridge export and try to infer district/provider sinks in `Cities2-MCP`.

- Pros: no mod-side schema change
- Cons: impossible with the current payload; the bridge does not expose enough detail

## Recommendation

Use Option 2.

The bridge should stay honest about what is InfoLoom-backed and what is bridge-owned runtime analysis. The existing `panels` object remains a mirror of validated InfoLoom state. New commuter-destination data should live under a bridge extension namespace so users and agents can tell where it came from.

## Export Contract

Keep the current top-level metadata and panels, then add:

```json
{
  "bridge_extensions": {
    "commute_destinations": {
      "source_component": "ecs.commute_destinations:Game.Companies.WorkProvider|Game.Companies.Employee|Game.Areas.CurrentDistrict|Game.Buildings.Building|Game.Prefabs.PrefabRef|Game.Prefabs.WorkplaceData|Game.Prefabs.IndustrialProcessData",
      "notes": [
        "commute destinations are bridge-owned runtime summaries built from active work providers and employee buffers",
        "outside-connection origin attribution is not included in this export"
      ],
      "by_district": [
        {
          "district_entity": 101,
          "district_name": "Industrial Park",
          "provider_count": 84,
          "jobs_total": 4210,
          "jobs_filled": 3880,
          "jobs_open": 330,
          "commuter_employees": 1704,
          "local_employees": 2176,
          "sector_commuter_employees": {
            "service": 0,
            "commercial": 0,
            "leisure": 0,
            "extractor": 0,
            "industrial": 1610,
            "office": 94
          }
        }
      ],
      "top_work_providers": [
        {
          "provider_entity": 5001,
          "building_entity": 4100,
          "district_entity": 101,
          "district_name": "Industrial Park",
          "building_name": "North Freight Campus",
          "company_name": null,
          "sector": "industrial",
          "jobs_total": 220,
          "jobs_filled": 213,
          "jobs_open": 7,
          "commuter_employees": 184,
          "local_employees": 29
        }
      ],
      "provider_rows_total": 1842,
      "provider_rows_exported": 200,
      "provider_rows_truncated": true
    }
  }
}
```

## Why This Shape

- `by_district` stays small and complete, because district counts are naturally bounded.
- `top_work_providers` answers the user's practical question without forcing the bridge to dump every provider row into `latest.json`.
- `provider_rows_total` and `provider_rows_truncated` make truncation explicit so MCP and agents do not mistake the ranking for a full table.
- `district_entity`, `provider_entity`, and `building_entity` preserve stable ids even when names are missing or ambiguous.

## Runtime Collection Design

Add a new bridge-owned runtime scan path alongside the existing InfoLoom adapter.

Responsibilities:

- Get the live `EntityManager` from the current game world.
- Iterate active work providers and their employee buffers.
- Reuse the existing provider-sector logic already proven in `Cities2-DataExport`:
  - service
  - commercial
  - leisure
  - extractor
  - industrial
  - office
- Count commuter vs local employees per provider.
- Resolve each provider's destination district from its building-side district carrier.
- Aggregate district totals.
- Sort providers by `commuter_employees` descending and export only the highest-value rows.

This should be a bridge runtime scan, not an InfoLoom reflection read, because the needed district/provider detail is not present in the current InfoLoom panel payloads.

## District Attribution

District attribution should follow the strongest proven structural path already used elsewhere in the project:

- prefer building-linked district carriers
- use `Game.Areas.CurrentDistrict` when present on the relevant building-side entity
- keep `district_entity` even if a display name is unavailable
- if no district is resolved, export `district_entity = null` and `district_name = null` rather than inventing a label

This keeps the export honest and still useful for comparing clusters.

## Provider and Naming Rules

The provider row contract should guarantee:

- stable entity ids
- sector classification
- commuter/local/total job counts
- destination district context

Names are best-effort only:

- `building_name` may be resolved when the runtime name path is cheap and trustworthy
- `company_name` may remain `null` in v1 if there is no clean, low-risk runtime carrier

The ranking is still valuable even when names are sparse, because the district and sector columns should already confirm or falsify the industrial-park hypothesis.

## Performance Guardrails

- Run on the same coarse bridge publish cadence.
- Reuse a single runtime scan per publish, not one scan per MCP request.
- Export all district rows, but cap provider rows to a fixed top-N such as `200`.
- If the scan cannot run, preserve the normal bridge file and emit a readable failure for the extension rather than crashing the mod.

The heavy work is the ECS scan, not the file write. A ranked export keeps payload size and serialization cost bounded.

## Error Handling

If the new commute scan fails:

- keep top-level bridge `status = "ok"` if the existing InfoLoom panels are still healthy
- write the failure inside `bridge_extensions.commute_destinations`, for example:
  - `status = "error"`
  - `message = "..."`
  - empty `by_district`
  - empty `top_work_providers`

This avoids turning a secondary analysis slice into a total bridge outage.

## Testing Strategy

Add focused bridge tests for:

- snapshot and writer coverage for the new `bridge_extensions.commute_destinations` shape
- coordinator coverage to prove commute data survives the normal `latest.json` write path
- runtime collector aggregation:
  - provider rows roll up into district totals
  - commuter vs local counts are correct
  - sector totals land in the expected district bucket
  - top provider truncation is explicit and deterministic
- failure payload behavior when the commute scan is unavailable

The existing InfoLoom compatibility tests should remain intact and independent from the new commute scan.

## MCP Implication

`Cities2-MCP` should treat this as a bridge extension, not as a fourth InfoLoom panel. MCP can expose it with ergonomic tools, but it should keep the runtime source boundary explicit in tool names, docs, and the skill.

## Success Criteria

This design is successful when:

- the bridge still exports the original three InfoLoom-backed panels unchanged
- a running city produces district rows that show where commuters are going
- the export highlights the top commuter-heavy work providers
- industrial vs office commuter concentration is visible by district and provider
- the bridge does not claim to know outside-connection origin yet
