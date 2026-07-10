<h1 align="center">Atya.Tooling.Cli</h1>

<p align="center"><i>Read-only diagnostics and release-state checks for Atya package repositories.</i></p>

<p align="center">
  <a href="https://www.nuget.org/packages/Atya.Tooling.Cli"><img src="https://img.shields.io/nuget/v/Atya.Tooling.Cli?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&color=512BD4" alt="NuGet Version"></a>
  <a href="https://github.com/AtyaLibraries/Cli/actions"><img src="https://img.shields.io/github/actions/workflow/status/AtyaLibraries/Cli/ci.yml?branch=development&style=for-the-badge&logo=githubactions&logoColor=white&label=Build" alt="Build"></a>
  <img src="https://img.shields.io/badge/.NET_10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt="Target Framework">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/AtyaLibraries/Cli?style=for-the-badge&color=512BD4" alt="License"></a>
</p>

## Overview

`atya` turns the Atya Package Constitution into deterministic command-line checks. It is built for maintainer workflows, package CI, and contributors who need to validate a repository before opening a PR.

The v1 tool is read-only. It diagnoses repository conformance, release preflight state, and post-release state assertions. It never scaffolds, fixes, tags, publishes, writes GitHub state, or handles secrets.

## Installation

```bash
dotnet tool install --global Atya.Tooling.Cli
```

For repo-local use:

```bash
dotnet new tool-manifest
dotnet tool install Atya.Tooling.Cli
dotnet tool run atya -- doctor
```

## Commands

```bash
atya --version
atya doctor
atya release check --online --version <vNew>
atya release verify --online --version <vNew>
```

Global options:

```bash
--format text|json
--working-directory <path>
--verbose
--no-color
--ci
--warnings-as-errors
--online
```

Network checks are disabled by default. `--online` is required for NuGet and GitHub state checks.

## Refused v1 Commands

The following surfaces are intentionally refused in v1:

```bash
atya init
atya add ...
atya pack
atya clean
atya generate ...
atya publish
atya tag
```

Scaffolding belongs to `dotnet new atya-nuget`; publishing belongs to the protected `v*` tag to central OIDC publisher chain.

## JSON Output

```bash
atya doctor --ci --format json
```

JSON output is machine-only and does not mix human text:

```json
{
  "status": "passed",
  "workingDirectory": "C:\\repo",
  "constitutionVersion": "1.2.0",
  "summary": {
    "passed": 51,
    "warnings": 0,
    "errors": 0,
    "skipped": 3
  },
  "findings": []
}
```

Each finding has `{ code, severity, message, file, recommendation }`.

## Diagnostic Groups

| Group | Constitution section |
|---|---|
| `REPO-*` | `repoStructure` |
| `SDK-*` | `targetFrameworkPolicy`, `solutionStrategy` |
| `NAME-*` | `naming` |
| `CPM-*` | `dependencyRules` |
| `CI-*` | `ciCd` |
| `PKG-*` | `packaging` |
| `REL-*` | `versioningAndReleasePolicy` |

## Constitution Precedence

The CLI follows `platform/specs/ATYA-CLI.md`, with `platform/CONSTITUTION.md` taking precedence where they disagree.

Known v1 discrepancies:

- The CLI spec says the benchmark project may be deleted for this infra repo. The constitution says each repo contains the benchmark project, so this repo keeps `benchmarks/Cli.Benchmarks`.
- The CLI spec marks a missing `PackageValidationBaselineVersion` as `PKG-006` error. The constitution says the baseline names the latest already-published stable package and is bumped after publish; a first release has no valid baseline. v1 reports this as a warning until the first stable publish.

## Exit Codes

| Code | Meaning |
|---:|---|
| 0 | Success |
| 1 | Validation failed |
| 2 | Invalid arguments |
| 3 | External command failed |
| 4 | Not an Atya repository |
| 10 | Unhandled exception |

## Development

```bash
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release
dotnet format --verify-no-changes
dotnet pack -c Release
```

## License

Released under the MIT license. See [LICENSE](LICENSE).
