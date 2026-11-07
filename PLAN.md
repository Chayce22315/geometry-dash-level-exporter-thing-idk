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
- provider failures are isolated and visible\n- research completion report shows per-provider successes, failures, and empty-result messages
- only documented or authorized access; no access-control circumvention

### discovery engine
- query planning from id, name, creator, song, date, and discovered references\n- broader internet archive term matching and exact numeric-id metadata queries\n- github repository search is not artificially restricted to readme-only matches
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
- initial live internet archive metadata provider\n- live github public repository metadata provider\n- live duckduckgo public web-search adapter with conservative request spacing\n- live reddit public search adapter for public discussion leads\n- live youtube public search-page adapter for public video leads

### m3: clue intelligence (implemented; build confirmed by user)
- conservative entity extraction for level ids, song ids, creator labels, videos, archive records, repositories, and web references
- normalized graph entities and deterministic graph identifiers
- relationship extraction with human-readable rationale
- cross-domain corroboration without treating repeated metadata as recovered level data
- duplicate-source detection that preserves every original clue
- native wpf evidence graph viewer with source/rationale tooltips
- automatic graph refresh after research, manual clue capture, and file inspection

### m4: autonomous investigations (implemented, build verification pending)
- deep research is the default behavior for every research run; no shallow mode
- multi-round query expansion from the original target and newly discovered clues
- every query is routed through every registered provider, reusing fresh cached responses when available
- bounded investigation depth and query count to prevent runaway searches
- provider pagination for up to 150 archive metadata records per query
- 12-hour response caching and source-level deduplication
- live progress reporting and cancellation
- checkpoint after every completed query
- persisted pending-query queue, so cancelled or interrupted research can resume
- saved research status and history counters
- automatic evidence graph refresh after a completed run

### m5: recovery (initial implementation)
- inspect original .gmd/plist/xml files and decode the k4 payload
- detect gzip by magic bytes and safely decompress with a 64 MiB expanded-size limit
- validate the comma-separated object key/value structure without rewriting source bytes
- scan local clue file paths and public Internet Archive item metadata for .gmd/.plist/.xml candidates
- download only supported candidate file types from archive.org, with a 25 MiB cap
- preserve source URL, retrieval time, raw SHA-256, payload SHA-256, level name/id, and validation status
- deduplicate identical source files by SHA-256
- distinguish validated payloads from partial/unvalidated candidates
- never treat metadata, screenshots, videos, or arbitrary pages as recovered level data
- remaining: historical version comparison, broader authorized source adapters, fixtures and automated recovery tests

### m6: export studio (initial implementation)
- native WPF export studio lists recovery candidates and permits selection
- export only candidates already marked validated; re-inspect the source before writing
- preserve original candidate bytes unchanged rather than reconstructing or fabricating level data
- write an adjacent JSON provenance manifest with source URL, original format, level metadata, hashes, and validation disclaimer
- re-inspect exported file and compare raw SHA-256; remove a failed output
- remaining: automated round-trip tests, raw-response export, investigation-report export, batch export, and broader format fixtures

### m7: hardening and release (implementation in progress)
- add a dedicated automated test project to the solution
- cover valid plist payloads, gzip decompression, malformed base64, malformed object pairs, non-plist inputs, and public provider input validation
- verify export refuses partial candidates and preserves original bytes with a provenance manifest
- run restore, build, and tests in windows github actions
- publish a self-contained single-file win-x64 app artifact for main-branch pushes
- remaining: provider failure simulations, recovery network edge cases, accessibility/performance review, clean-machine artifact verification, versioned release notes, and signed/reproducible release packaging

## evidence confidence

confidence must be explainable and must not imply more than the evidence supports.

- **unverified:** user-entered or uncorroborated clue
- **corroborated:** the same extracted reference appears on at least two distinct source domains; this confirms repetition, not truth
- **strong match:** reserved for exact canonical duplicate source references in this phase
- **data recovered:** original level payload located
- **validated:** payload passes applicable format and integrity checks

metadata confidence and level-data validation are separate dimensions.

## phase 4 research policy

Every run uses the deep-research pipeline. It starts from the original query, expands into game-context and explicitly identified entity searches, then creates follow-up queries from newly discovered source records. The current safety bounds are 24 queries per run, depth 4, and up to 3 pages of 50 metadata records per Internet Archive query. A later run resumes saved pending queries rather than silently restarting completed work. The current live providers are Internet Archive metadata search, GitHub public repository search, DuckDuckGo public HTML web search, Reddit public discussion search, and YouTube public video search; the engine fans out to every provider registered in the application. The DuckDuckGo and YouTube HTML interfaces are not stable APIs and may change or rate-limit requests. Reddit may deny or rate-limit unauthenticated public search. These sources provide leads only, not recovered level data. Deep research is thorough within configured public sources, not a claim of crawling the entire internet.

## phase 7 verification boundary\n\nA passing compile is not the same as a passing test suite or a verified release. Phase 7 is complete only after the Windows CI workflow passes its automated tests and successfully publishes an artifact that has been checked on a clean Windows environment. The current test fixtures verify format behavior; they do not prove that a recovered file is authored by a claimed creator or that every possible Geometry Dash payload variant is supported.\n\n## current implementation boundary

phase 3 extracts only clearly labeled identifiers and references from clue text. it does not infer creator identity from arbitrary names, inspect video contents, claim archive metadata is a level payload, or fabricate missing data. provider research remains limited to documented or authorized access.
