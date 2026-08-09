# HwSync Architecture Decisions

## ADR-001 — .NET version

**Status:** Accepted  
**Date:** 2026-08-09

### Decision

HwSync uses .NET 10 LTS as its primary target framework.

### Rationale

HwSync is a new project and is also intended as a practical environment
for learning modern .NET development.

There is no requirement to maintain compatibility with legacy .NET versions.

All new HwSync projects should target:

`net10.0`