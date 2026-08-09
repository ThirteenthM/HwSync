# HwSync — AI Project Context

## Project

HwSync is a personal cross-platform synchronization system written in C#/.NET.

The project is being developed incrementally, starting from a very small working synchronization core and evolving only when real requirements appear.

The project is also intended as a practical environment for learning and applying modern .NET technologies and software architecture.

## Main Goal

Create a synchronization system capable of synchronizing files and, potentially in the future, other types of data between multiple devices through a central server.

The system should eventually support:

- Windows
- Linux
- Android
- Apple platforms

## Server

Initial server platform:

- Windows

Possible future server platform:

- Linux

Initial persistence may use:

- SQLite

Future server persistence may use:

- PostgreSQL

Changing the operating system or database must not require rewriting the synchronization core.

## Architecture Principles

### 1. Platform-independent core

HwSync.Core must not depend on:

- Windows
- Linux
- Android
- Apple-specific APIs
- SQLite
- PostgreSQL
- concrete network transport
- concrete file-system monitoring implementation

### 2. Interfaces and implementations are separated

Contracts/interfaces and their implementations should reside in separate projects where practical.

Expected projects include:

- HwSync.Abstractions
- HwSync.Core
- HwSync.Infrastructure
- HwSync.Server
- HwSync.Client
- HwSync.Tests

Additional platform-specific projects will be introduced only when required.

### 3. Hide expected change behind abstractions

If during design we say:

"we will implement it this way now, but may replace it later"

that area should normally be isolated behind an interface or another explicit abstraction boundary.

Do not, however, create interfaces mechanically for every class.

### 4. File change detection is replaceable

The synchronization engine must not depend directly on FileSystemWatcher.

Possible implementations may include:

- directory scanning
- FileSystemWatcher
- NTFS USN Journal
- hybrid detection

FileSystemWatcher should be treated as a notification/optimization mechanism rather than the authoritative source of file-system state.

### 5. Cross-platform paths

Synchronization protocol and core models must not store operating-system-specific absolute paths.

Internal paths should use a normalized relative representation, for example:

Photos/2026/Test.jpg

Platform-specific code maps this representation to the local file system.

### 6. Development approach

Build the system in small, working increments.

Do not implement functionality merely because it may be useful someday.

Prefer:

- simple first implementation
- clear abstraction boundary
- tests
- replacement later when justified

over premature complexity.

## Initial Milestone

The first milestone is intentionally small:

1. Scan a directory.
2. Build a snapshot of its files.
3. Compare two snapshots.
4. Detect:
   - Created
   - Modified
   - Deleted
5. Cover comparison logic with unit tests.

No server, networking, UI or database is required for this milestone.

## AI Collaboration

Important project context is stored in the repository so that different AI tools can work with the project without depending on a specific chat history.

Relevant files:

- docs/AI-CONTEXT.md
- docs/ARCHITECTURE.md
- docs/DECISIONS.md
- docs/ROADMAP.md

Before making architectural changes, AI assistants should read these documents.

AI-generated changes should preserve established architectural decisions unless a change is explicitly discussed and accepted.