# provider implementation guide

every source integration belongs in its own implementation of `IArchiveProvider`.

## required behavior

- state exactly which public or authorized endpoint is used
- declare supported search and retrieval capabilities
- use cancellation tokens
- apply conservative rate limits and caching
- preserve the source url and retrieval timestamp
- return a failed response with a useful message instead of hiding errors
- never attempt to bypass authentication, cloudflare, or other access controls
- never claim a metadata result contains level data unless the original payload is actually present

## test requirements

each provider should include fixtures for:
- a successful result
- no results
- malformed responses
- throttling / rate-limit responses
- unavailable source
- partial metadata

network integration tests must be opt-in. unit tests should use saved fixtures and never depend on a live service.
