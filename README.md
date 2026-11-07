# levelvault

**a geometry dash clue finder, archive research workspace, original-level recovery tool, and provenance-aware export studio.**

levelvault investigates public references to geometry dash levels, organizes evidence, and helps locate surviving original level data. its long-term goal is to validate and export authentic `.gmd` files.

current status: native windows desktop app. phases 1–6 have been build-confirmed by the user. phase 7 adds automated validation tests and a self-contained windows release artifact through github actions. current live research sources include an exact-id Boomlings server lookup, internet archive metadata, github public repository search, duckduckgo public web search, reddit public discussion search, and youtube public video search. these adapters broaden discovery but do not crawl the entire internet.

## what exists now

- native windows desktop shell built with wpf / .net 8
- create and persist multiple investigations locally
- universal clue input: level id, title, creator, song id, date, or mixed clues
- curated search launch set for bing, duckduckgo, youtube, tiktok, github, reddit, indexed public discord links, and community references
- manually capture clue title, source url, and notes
- import and structurally inspect `.gmd` / plist files
- keep evidence and investigation history in local app data
- provider-independent service boundaries with a dedicated Boomlings exact-id metadata adapter plus internet archive metadata, github public repository search, duckduckgo web search, reddit discussion search, and youtube video search adapters
- automatic source-linked clue collection, duplicate suppression, and a versioned 12-hour local response cache
- conservative request spacing, cancellation support, and provider health checks
- phase 3 entity extraction for explicitly labeled level ids, song ids, creators, and discovered urls
- relationship graph with source-linked rationale and conservative cross-domain corroboration
- exact-source duplicate detection that preserves all original clues
- native wpf evidence graph viewer with hover details
- phase 4 always-on deep research: multi-round query expansion from the target and newly discovered clues
- every deep-research query is routed through every registered provider, reusing fresh cached responses when available (internet archive, github repositories, duckduckgo web search, reddit, and youtube)
- up to 36 queries per run, five follow-up levels, and up to 250 archive metadata results per query
- live progress and cancellation controls
- automatic checkpoints, saved pending-query queues, and resume after cancellation or interruption
- saved research status and query history
- phase 5 recovery candidates with original source URLs, retrieval timestamps, validation state, and raw/payload SHA-256 hashes
- phase 6 export studio for selecting a validated candidate and exporting its original bytes as `.gmd`
- automatic adjacent JSON provenance manifest containing source, hashes, level metadata, and validation notes
- re-inspection before export and round-trip verification of the exported copy; failed copies are removed
- phase 7 automated tests for valid plist payloads, gzip payloads, malformed base64, malformed object pairs, non-plist input, and safe export behavior
- provider diagnostics report per-source success, failure, and empty-result messages after each research run
- explicit empty-state guidance replaces the blank clue-table appearance
- reddit public search collects public discussion links and metadata without assuming post authors created the level
- youtube public search collects public video links, titles, and channel names as leads
- evidence graph recognizes user-supplied tiktok links as video leads and discord/reddit links as discussion leads without attempting to access private content
- internet archive query expansion supports broader word matching and exact numeric-id searches
- github repository searches are no longer restricted to readme-only matches
- github actions runs the test suite and publishes a self-contained win-x64 app artifact on pushes to main

## phase 7 status

automated test coverage and release packaging are implemented. the latest windows workflow (run #109) passed restore, build, all automated tests, self-contained win-x64 publishing, and artifact upload. the `LevelVault-win-x64` artifact is available from the workflow run and expires november 21, 2026. clean-machine verification and versioned release notes remain before calling the release fully verified. clean-machine verification and versioned release notes remain before calling the release fully verified.

## phase 3 status

implemented; the user confirmed the windows build succeeded. graph entities are normalized, relationships carry confidence explanations, and repeated references are corroborated only when they appear across distinct source domains. exact duplicate source urls are linked but never deleted. a corroborated reference is not proof of authorship or recovered level data.

## phase 4 status

implemented; the user confirmed the windows build succeeded. every research run uses the deep-research pipeline. it expands the original query, follows newly discovered clues, checks every registered provider, reports progress, and saves a resumable queue after each completed query. the current run limits are 36 queries and five follow-up levels. internet archive searches paginate up to five pages of 50 records each. github repository search requests up to 50 results and reddit public search requests up to 50 results per query. providers are queried concurrently per research round while each provider retains its own request-spacing controls. internet archive metadata, github public repository search, duckduckgo public web search, reddit public discussion search, and youtube public video search are currently registered. duckduckgo and youtube public html results are external interfaces that may change or rate-limit requests. reddit may deny or rate-limit unauthenticated public search. broader coverage still requires additional documented or authorized source adapters.

## remaining improvements

- add more documented public-source adapters and historical-version comparison
- add additional recovery fixtures and network-edge-case tests
- verify the release artifact on a clean windows machine and prepare versioned release notes

## requirements

- windows 10/11 (windows 11 recommended)
- .net 8 sdk
- visual studio 2022 with the .net desktop development workload, or the dotnet cli

## run

```powershell
dotnet build .\\LevelVault.sln
dotnet run --project .\\src\\LevelVault\\LevelVault.csproj
```

## local data

investigations are stored under:

```
%LOCALAPPDATA%\\LevelVault\\investigations
```

each investigation is a readable json file. investigations and cache files remain local. when archive research is started, search clues are sent to registered providers. the Boomlings adapter only sends an exact numeric level id to the public type-10 level lookup; other generic searches are skipped by that adapter. other providers search their respective public sources. successful responses are cached locally for 12 hours.

## important recovery rules

- metadata, screenshots, videos, and discussion posts are clues, not level data.
- a valid plist container is not proof that a file is complete or authentic.
- only recovered original level data may be exported as an authentic level.
- missing objects must never be invented to make an export appear complete.
- the Boomlings adapter uses the documented `getGJLevels21.php` type-10 exact-id lookup and conservative request spacing. it records current metadata only; it does not treat an empty or `-1` response as proof that a level never existed.
- providers must use documented or authorized access methods and respect rate limits.
- levelvault does not bypass cloudflare, authentication, or other access controls.

## project structure

```
src/LevelVault/
  Models/       investigation, clue, and evidence graph records
  Services/     local storage, research, analysis, search planning, gmd validation and export
  Providers/    public archive adapters
  Views/        native evidence graph and export studio windows
  App.xaml      shared theme resources
  MainWindow    native desktop workspace
tests/
  LevelVault.Tests/ automated recovery and export tests
```

## roadmap

1. foundation and local investigation workspace
2. provider framework and initial public archive integration
3. clue extraction, correlation, and graph (implemented; build confirmed by user)
4. autonomous, resumable deep investigations (implemented; build confirmed by user)
5. original level-data recovery (implemented; build confirmed by user)
6. provenance-aware .gmd export studio (implemented; build confirmed by user)
7. hardening, automated tests, and self-contained windows packaging (in progress)

see [PLAN.md](PLAN.md) for the full development plan.

## license

mit
