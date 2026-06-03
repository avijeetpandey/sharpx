# Changelog

All notable changes to **SharpX** are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/).

## [0.1.0] - Initial release

### Added
- `SharpXClient` with `GetAsync`, `PostAsync`, `PutAsync`, `PatchAsync`, `DeleteAsync`, `HeadAsync`, `OptionsAsync`.
- Strongly-typed `SharpXRequestConfig` and `SharpXResponse<T>`.
- Request and response interceptor system (`use` / `eject` / `clear`).
- Automatic JSON serialization with `System.Text.Json` and configurable options.
- `UrlEncodedFormData` and `MultipartFormData` payload helpers.
- Robust query-string serialization for objects, dictionaries, and arrays.
- `TransformRequest` / `TransformResponse` hooks.
- Per-call and per-instance timeouts plus first-class `CancellationToken` support.
- `SharpXException` with categories (`HttpStatus`, `Timeout`, `Cancelled`, `Network`, `Deserialization`, `Interceptor`, `InvalidConfiguration`).
- Header CRLF/NUL injection guard and sensitive header/URL redaction.
- Custom `ValidateStatus` predicate to choose which status codes throw.
- Console examples in `samples/SharpX.Examples`.
- Unit, integration (WireMock.Net), and end-to-end test suites.
