# Atya.Tooling.Cli

Read-only diagnostics and release-state checks for Atya package repositories.

[![NuGet Version](https://img.shields.io/nuget/v/Atya.Tooling.Cli?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&color=512BD4)](https://www.nuget.org/packages/Atya.Tooling.Cli)
[![Build](https://img.shields.io/github/actions/workflow/status/AtyaLibraries/Cli/ci.yml?branch=development&style=for-the-badge&logo=githubactions&logoColor=white&label=Build)](https://github.com/AtyaLibraries/Cli/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-512BD4?style=for-the-badge)](https://github.com/AtyaLibraries/Cli/blob/development/LICENSE)

## Install

```bash
dotnet tool install --global Atya.Tooling.Cli
```

## Quick Start

```bash
atya doctor --ci
atya doctor --ci --format json
atya release check --online --version 0.1.0
atya release verify --online --version 0.1.0
```

## Feature Tour

- `atya doctor` validates repository structure, SDK pinning, naming, central package management, workflows, package metadata, and release policy diagnostics.
- `atya release check` runs the online release preflight for a proposed version.
- `atya release verify` verifies the four post-release state assertions from the Atya constitution.
- `--online` is required before the tool accesses NuGet or GitHub.
- `--format json` emits machine-readable JSON only.

## Command Surface

```bash
atya --version
atya doctor
atya release check --online --version <vNew>
atya release verify --online --version <vNew>
```

Refused in v1: `init`, `add`, `pack`, `clean`, `generate`, `publish`, and `tag`.

## JSON Contract

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

## Error Codes

The CLI reports diagnostic codes, not domain error codes.

| Group | Meaning |
|---|---|
| `REPO-*` | Repository layout and forbidden files |
| `SDK-*` | SDK pinning and project shape |
| `NAME-*` | Package naming |
| `CPM-*` | Central package management and lock files |
| `CI-*` | Workflow and publisher-chain checks |
| `PKG-*` | Package metadata |
| `REL-*` | Release state |

## Links

- Repository: https://github.com/AtyaLibraries/Cli
- Issues: https://github.com/AtyaLibraries/Cli/issues
- License: https://github.com/AtyaLibraries/Cli/blob/development/LICENSE
