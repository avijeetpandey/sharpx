# Contributing to SharpX

Thanks for your interest in improving SharpX!

## Workflow

1. Fork and create a feature branch (`feat/short-description`, `fix/...`, `docs/...`).
2. Run `dotnet build -c Release` and `dotnet test -c Release` locally.
3. Keep changes focused. Add or update tests for any behavioural change.
4. Submit a pull request describing the motivation and the change.

## Coding standards

- Target both `netstandard2.1` and `net8.0`.
- Treat warnings as errors; do not introduce `#pragma` suppressions casually.
- Public APIs require XML documentation comments.
- Prefer composition over inheritance.
- Sensitive data (Authorization, Cookies, tokens) must always go through the redactor before logging or being attached to exceptions.

## Tests

- Unit tests live under `tests/SharpX.UnitTests` and must not require a network.
- HTTP behaviour is exercised against [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) in `tests/SharpX.IntegrationTests`.
- End-to-end flows live in `tests/SharpX.E2ETests`.
