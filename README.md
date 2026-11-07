# levelvault

**a geometry dash clue finder, archive research workspace, and original-level recovery tool.**

levelvault investigates public references to geometry dash levels, organizes evidence, and helps locate surviving original level data. its long-term goal is to validate and export authentic `.gmd` files.

> current status: early windows desktop build. phase 3 adds conservative clue intelligence and a native evidence graph. phase 2 includes one live public archive adapter. this is not an internet-wide crawler or a level downloader.

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
- phase 3 entity extraction for explicitly labeled level ids, song ids, creators, and discovered urls
- relationship graph with source-linked rationale and conservative cross-domain corroboration
- exact-source duplicate detection that preserves all original clues
- native wpf evidence graph viewer with hover details

## phase 3 status

implemented in source; awaiting the automated windows build. graph entities are normalized, relationships carry confidence explanations, and repeated references are corroborated only when they appear across distinct source domains. exact duplicate source urls are linked but never deleted. a corroborated reference is not proof of authorship or recovered level data.

## planned capabilities

- independently implemented archive/provider adapters
- public-source discovery and query expansion
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
dotnet build .\\LevelVault.sln
dotnet run --project .\\src\\LevelVault\\LevelVault.csproj
```

## local data

investigations are stored under:

```
%LOCALAPPDATA%\\LevelVault\\investigations
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
  Models/       investigation, clue, and evidence graph records
  Services/     local storage, research, analysis, search planning, gmd validation
  Providers/    public archive adapters
  Views/        native evidence graph window
  App.xaml      shared theme resources
  MainWindow    native desktop workspace
```

## roadmap

1. foundation and local investigation workspace
2. provider framework and initial public archive integration
3. clue extraction, correlation, and graph (implemented, build verification pending)
4. autonomous, resumable investigations
5. level-string parsing, decompression, and validation
6. provenance-aware .gmd export
7. expanded archives, testing, and release packaging

see [PLAN.md](PLAN.md) for the full development plan.

## license

mit
