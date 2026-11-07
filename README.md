# levelvault

**a geometry dash clue finder, archive research workspace, and original-level recovery tool.**

levelvault investigates public references to geometry dash levels, organizes evidence, and helps locate surviving original level data. its long-term goal is to validate and export authentic `.gmd` files.

 > current status: early windows desktop build. phase 2 includes one live public archive adapter. this is not yet an internet-wide crawler or a level downloader.

## what exists now

- native windows desktop shell built with wpf / .net 8
- create and persist multiple investigations locally
- universal clue input: level id, title, creator, song id, date, or mixed clues
- curated search launch set for bing, duckduckgo, youtube, github, reddit, and community references
- manually capture clue title, source url, and notes
- import and structurally inspect `.gmd` / plist files
- keep evidence and investigation history in local app data
- provider-independent service boundaries and a live internet archive metadata search adapter
- automatic source-linked clue collection, duplicate suppression, and a 12-hour local response cache
- conservative request spacing, cancellation support, and provider health checks

## phase 2 status

implemented: provider contract and registry, internet archive public metadata search, normalized source-linked clues, local caching, rate limiting, cancellation, and health checks.

still planned: more archive adapters, multi-hop query expansion, evidence graph visualization, original level payload retrieval, decompression, and authentic `.gmd` export. internet archive records are metadata leads, not recovered level files.

## planned capabilities

- independently implemented archive/provider adapters
- public-source discovery and query expansion
- evidence correlation and an interactive clue graph
- duplicate and historical-version comparison
- original level-string parsing and compression support
- integrity checks and provenance-aware exports
- resumable investigations and batch search

## requirements

- windows 10/11 (windows 11 recommended)
- .net 8 sdk
- visual studio 2022 with the .net desktop development workload, or the dotnet cli

## run

```powershell
dotnet build .\LevelVault.sln
dotnet run --project .\src\LevelVault\LevelVault.csproj
```

## local data

investigations are stored under:

```
%LOCALAPPDATA%\LevelVault\investigations
```

each investigation is a readable json file. investigations and cache files remain local. when archive research is started, the search clue is sent to internet archive to perform the search. successful responses are cached for 12 hours.

## important recovery rules

- metadata, screenshots, videos, and discussion posts are clues, not level data.
- a valid plist container is not proof that a file is complete or authentic.
- only recovered original level data may be exported as an authentic level.
- missing objects must never be invented to make an export appear complete.
- providers must use documented or authorized access methods and respect rate limits.
- levelvault does not bypass cloudflare, authentication, or other access controls.

## project structure

```
src/LevelVault/
  Models/       investigation and clue records
  Services/     local storage, search planning, gmd validation
  App.xaml      shared theme resources
  MainWindow    native desktop workspace
```

## roadmap

1. foundation and local investigation workspace
2. provider framework and initial public archive integration (in progress)
3. clue extraction, correlation, and graph
4. autonomous, resumable investigations
5. level-string parsing, decompression, and validation
6. provenance-aware .gmd export
7. expanded archives, testing, and release packaging

see [PLAN.md](PLAN.md) for the full development plan.

## license

mit
