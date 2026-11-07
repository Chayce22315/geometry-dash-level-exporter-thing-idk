# levelvault development plan

## product definition

levelvault is a general-purpose geometry dash investigation and recovery platform. a user supplies any clue; the app searches configured public sources, connects evidence, locates surviving original level data, validates it, and exports an authentic `.gmd` when possible.

the application must work for arbitrary targets. no level id, creator, song, or title is hardcoded.

## architecture

### desktop application
- native windows 11 application using c# / .net and wpf
- local-first storage
- investigation workspace, search, clue graph, archive explorer, recovery workspace, export studio, settings

### investigation domain
- investigations, clues, entities, relationships, candidate level records
- every clue stores source, retrieval time, notes, and verification state
- contradictions remain visible rather than being silently resolved

### provider system
- one adapter per source
- common provider contract for search, lookup, capabilities, status, and rate limits
- source-specific parsing stays inside its adapter
- provider failures are isolated and visible
- only documented or authorized access; no access-control circumvention

### discovery engine
- query planning from id, name, creator, song, date, and discovered references
- deduplication and query expansion
- configurable search depth and cancellation
- provenance for every result

### clue graph
- nodes: levels, creators, songs, pages, videos, screenshots, archive records, files, versions
- edges: created-by, uses-song, references, archived-at, contains-data, possible-duplicate, earlier-version
- every edge has evidence and confidence rationale

### recovery pipeline
1. identify a candidate source record
2. preserve the raw response unchanged
3. parse known response/container formats
4. extract compressed level payloads
5. validate structure and available integrity fields
6. compare independent copies
7. label completeness and provenance
8. export only original data

### export formats
- gmd / plist where format support is verified
- raw server response
- metadata json
- investigation report

## milestones

### m1: foundation
- native desktop shell
- local investigation persistence
- generic search launch set
- manual clue capture
- gmd/plist structural inspection

### m2: provider framework
- typed provider contract
- provider registry and settings
- health/status reporting
- caching and rate-limit policy
- integration tests with saved fixtures
- initial live internet archive metadata provider

### m3: clue intelligence (implemented, build verification pending)
- conservative entity extraction for level ids, song ids, creator labels, videos, archive records, repositories, and web references
- normalized graph entities and deterministic graph identifiers
- relationship extraction with human-readable rationale
- cross-domain corroboration without treating repeated metadata as recovered level data
- duplicate-source detection that preserves every original clue
- native wpf evidence graph viewer with source/rationale tooltips
- automatic graph refresh after research, manual clue capture, and file inspection

### m4: autonomous investigations
- query expansion
- provider orchestration
- progress, cancellation, resume
- investigation report

### m5: recovery
- response parsers
- compression/decompression fixtures
- integrity validation
- historical version comparison
- partial-data diagnostics

### m6: export studio
- gmd writer for verified formats
- raw response export
- metadata and provenance manifest
- round-trip tests

### m7: hardening and release
- automated test suite
- provider failure simulations
- malformed input tests
- accessibility and performance
- signed/reproducible release packaging

## evidence confidence

confidence must be explainable and must not imply more than the evidence supports.

- **unverified:** user-entered or uncorroborated clue
- **corroborated:** the same extracted reference appears on at least two distinct source domains; this confirms repetition, not truth
- **strong match:** reserved for exact canonical duplicate source references in this phase
- **data recovered:** original level payload located
- **validated:** payload passes applicable format and integrity checks

metadata confidence and level-data validation are separate dimensions.

## current implementation boundary

phase 3 extracts only clearly labeled identifiers and references from clue text. it does not infer creator identity from arbitrary names, inspect video contents, claim archive metadata is a level payload, or fabricate missing data. provider research remains limited to documented or authorized access.
