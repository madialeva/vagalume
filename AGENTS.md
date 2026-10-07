# AGENTS.md — Vagalume

## Project

Vagalume is a **proof of concept** that grew out of **Arume**, a billing and
accounting product currently written in Java. It explores whether one .NET
application can run in **two modes with the same code**: as an autonomous
**desktop application** for an individual or a freelancer, and as a **web
service hosted by the company itself** that several users reach from a browser.
It may end up as a project fully detached from Arume.

The first question the proof of concept answers is the database: using
**PostgreSQL in both modes**, bundled inside the application and started as a
child process in the autonomous mode, so there is a single engine, a single SQL
dialect and a single chain of migrations. Other directions under evaluation
(none decided): Blazor Server as UI shared by both modes, Electron.NET or
Photino as desktop shell, EF Core with PostgreSQL only. The roadmap lives in
the GitHub issues and in the README.

## Stack and constraints

- **.NET 10** (SDK pinned in `global.json`), C# with nullable reference types
  enabled and warnings treated as errors (set in `Directory.Build.props`).
- xUnit for tests, Npgsql as the PostgreSQL driver, EF Core (PostgreSQL only) for
  persistence, Electron.NET Core as the desktop shell. Node.js 22 or later is
  needed to build and run the desktop hosts.
- Two UI architectures coexist so they can be compared. **Blazor Server**
  (`Vagalume.UI`, interactive server rendering only, Razor, no REST API between UI
  and logic) and **Blazor WebAssembly** (`Vagalume.Wasm.UI`, C# with a markup DSL
  and no Razor, talking to the server only through the REST API of `Vagalume.Api`;
  the client never references `Core`, `Data` or the database).
- PostgreSQL binaries come from the Zonky `embedded-postgres-binaries` artifacts
  on Maven Central, for **Linux x64 and Windows x64** only (no macOS, no ARM).
  They are downloaded **at build time**, pinned by version and SHA-256 in a lock
  file under `eng/`; the application never downloads anything at runtime.
- The embedded server listens only on `127.0.0.1`, uses no Unix socket and
  requires `scram-sha-256` authentication. Never introduce `trust`
  authentication.
- Do not add new NuGet packages (runtime or test) without an approved OpenSpec
  change.
- NuGet versions are managed centrally in `Directory.Packages.props`
  (Central Package Management): `PackageReference` items in projects carry no
  `Version`.
- Do not commit PostgreSQL binaries or any downloaded artifact.

## Commands

| Command                                   | Purpose                                       |
| ----------------------------------------- | --------------------------------------------- |
| `dotnet build`                            | Build the solution                            |
| `dotnet test`                             | Run all tests (integration tests start a real PostgreSQL) |
| `dotnet format --verify-no-changes`       | Check formatting and analyzers                |
| `dotnet format`                           | Apply formatting                              |
| `dotnet run --project src/Vagalume.Host.Web` | Run the web host (needs `ConnectionStrings__Vagalume`) |
| `dotnet run --project src/Vagalume.Host.Desktop` | Run the desktop host in an Electron window |
| `dotnet run --project src/Vagalume.Host.Wasm.Desktop` | Run the WebAssembly desktop host in an Electron window |
| `dotnet publish src/Vagalume.Host.Desktop -c Release -r linux-x64 --self-contained` | Package the Blazor Server desktop app (build on the target OS) |
| `dotnet publish src/Vagalume.Host.Wasm.Desktop -c Release -r linux-x64 --self-contained` | Package the WebAssembly desktop app (build on the target OS) |

Solution layout (`Vagalume.slnx`):

- `src/Vagalume.Core`: domain and use cases, depends on nothing.
- `src/Vagalume.Data`: EF Core, one migration chain; depends only on `Core`.
- `src/Vagalume.UI`: Razor Class Library with the shared UI; depends only on `Core`.
- `src/Vagalume.Host.Web`: composition root of the server host (external PostgreSQL).
- `src/Vagalume.Host.Desktop`: composition root of the desktop host (embedded
  PostgreSQL, Electron.NET, session-token guard).
- `src/Vagalume.Api.Contracts`: DTOs, routes, `INotesApi` and client exceptions of the
  REST API; depends on nothing.
- `src/Vagalume.Api`: minimal APIs over `Core`; depends on `Core` and the contracts.
- `src/Vagalume.Api.Client`: `HttpClient` implementation of `INotesApi`; depends only
  on the contracts.
- `src/Vagalume.Wasm.UI`: Blazor WebAssembly UI written in C# with a markup DSL and
  **no `.razor` or `.cshtml` files**; depends only on the contracts and the client,
  so the browser never receives `Core`, `Data` or the database.
- `src/Vagalume.Desktop.Hosting`: session token, local-port guard and PostgreSQL
  start/stop shared by both desktop hosts; knows nothing about Electron.
- `src/Vagalume.Host.Wasm.Desktop`: Electron host that serves the WASM app and the
  API from one local origin behind the session token.
- `src/Vagalume.Postgres.Embedded`: the embedded PostgreSQL host library.
- `tests/`: one project per source project, `Vagalume.Testing` (shared real
  PostgreSQL test environment) and `Vagalume.Architecture.Tests` (reads the
  `.csproj` files and fails on a forbidden reference).
- `eng/`: binaries lock file, the file-based script `fetch-postgres-binaries.cs`
  and `FetchPostgresBinaries.targets`, imported by every project that needs the
  binaries.

`dotnet build` fetches the PostgreSQL binaries of the current platform into
`artifacts/postgres/` (git-ignored). Integration tests carry the trait
`Category=Integration`; `dotnet test --filter Category=Integration` runs only
those that start a real PostgreSQL. Both hosts set `RequiresAspNetWebAssets`
because the Razor components live in `Vagalume.UI`; without it the Blazor client
script is missing. Tests are never aimed at the real user data directory.

Before handing off a change, run `dotnet format --verify-no-changes && dotnet
build && dotnet test` and keep a buildable tree.

## Code conventions

- File-scoped namespaces, one top-level type per file, file name equal to the
  type name. Root namespace `Vagalume`.
- Nullable reference types are on: never silence a warning with `!` unless the
  invariant is documented right there.
- Use `sealed` on classes not designed for inheritance (the default for new
  classes), `readonly` for fields that never change after construction and
  `record` types for immutable values.
- Interfaces use the .NET `I` prefix (`IClock`, not `Clock`).
- Async APIs return `Task`/`ValueTask`, end in `Async` and accept a
  `CancellationToken` as the last parameter. Never block on async code with
  `.Result` or `.Wait()`.
- Code, identifiers and comments in English.
- Every class has a short XML doc comment (`/// <summary>`, one to three lines,
  in English) right above it saying what it is for and, when it helps, which
  role it plays in the design (for example a Strategy or a Facade). The same
  goes for the interfaces that define a collaborator role. The name of a class
  is not always enough for someone who is just reading files. Apart from that,
  no comments unless they add information the code cannot express.
- Formatting is enforced by `.editorconfig` and `dotnet format`.
- Dependencies follow the latest stable versions that are compatible with each
  other; no compatibility paths for older .NET versions.
- Processes started by the code (`initdb`, `pg_ctl`, `pg_dump`…) are launched
  with an explicit argument list, never by building a shell command line from
  strings, and their output and exit code are always checked.

## Design philosophy

The code is **object-oriented first**, in the Java/C# tradition, and uses the
functional and structural features of C# where they are the better tool. This
is a deliberate choice of the author: keep it when adding code.

- **Objects model things with identity, state and lifecycle** (the embedded
  server host, a cluster, a backup…). State is encapsulated and the behavior
  lives with the data it works on. Avoid anemic data bags operated by static
  helpers.
- **Program to interfaces.** Collaborators (clock, process launcher, file
  system, port finder…) are injected through constructors as interfaces, so
  they can be substituted when a test needs it. Prefer **composition over
  inheritance**; inheritance only for a real is-a with shared behavior, at most
  two levels. Where behavior varies per platform (Linux vs Windows), use a
  Strategy object chosen at one point instead of `if (OperatingSystem…)`
  scattered through the code.
- **Dependency injection is manual constructor injection**, wired in a single
  composition root. The libraries do not depend on a DI container; an
  application project may use the one of the host it runs in
  (`Microsoft.Extensions.DependencyInjection`) only at its composition root.
- The author is experienced in Java/.NET OOP and delegates the choice of the
  most idiomatic, modern C# approach to the agent: recommend and justify when a
  different technique serves better than the classic OO one.
- **Use design patterns by name when they fit** (Strategy, State, Factory,
  Facade, Command), without ceremony.
- **Functional where it is the better tool:** pure static methods for stateless
  computation (parsing, argument building, path resolution); immutable data
  (`record`, `readonly`, `init`); LINQ pipelines instead of hand-written loops
  when clearer.
- **Layering:** the embedded-PostgreSQL library knows nothing about UI, web
  hosting or any business domain. Dependencies flow application → library,
  never the reverse.
- Every component is tested with the behavior it adds. Tests that start the
  real PostgreSQL are integration tests and are kept apart from pure unit tests.

## Language and files

- Use English for source, identifiers, comments, scripts, technical docs,
  commits and pull requests.
- GitHub issues and milestones: titles and descriptions always in English.
- OpenSpec artifacts (`proposal.md`, `design.md`, `tasks.md`, `specs/*.md`) and
  conversation with the user remain in **Spanish**.
- `README.md` is the public manual (English): what the project is, how to build
  and run it, and the findings report of the proof of concept. Update it when
  the structure, the commands or the findings change.
- This `AGENTS.md` is durable context versioned in the repository, so it
  survives clones/moves. `openspec/` (specs, changes and their archive) is
  versioned too: it is the project's design record, written in Spanish.
  `.claude/`, `.opencode/` and `.vscode/` are private working material and are
  git-ignored: do not reference them from published files such as `README.md`.
- `openspec/` is public: write proposals, designs and specs so that they stand
  on their own, without relying on private files of other repositories.

## OpenSpec

OpenSpec is the default workflow for changes. For each change: create
`proposal.md` and `design.md` (plus `tasks.md` and any required `specs/`
deltas), then **STOP** until the user approves proposal and design. Do not start
implementing tasks until then. Archive only after the user confirms.

This is not mandatory. If the programmer explicitly states that OpenSpec should
not be used (or that the change should be made directly), skip the workflow and
implement the change directly.

Artifact language: prose in `proposal.md`, `design.md`, `tasks.md` and
`specs/*.md` is written in Spanish, except the fixed labels imposed by the
OpenSpec skills, which stay in English as-is (`Why`, `What Changes`,
`Capabilities`, `Impact`, `Context`, `Goals`/`Non-Goals`, `Decisions`,
`Risks / Trade-offs`, `Migration Plan`, `Open Questions`, `Purpose`,
`ADDED`/`MODIFIED`/`REMOVED`/`RENAMED Requirements`, `Requirement:`,
`Scenario:`, `WHEN`/`THEN`/`AND`, `SHALL`/`MUST`). Code identifiers stay in
English inside the Spanish text.

## Working preferences

- Conversation with the user in Spanish; durable context in repo files (this
  `AGENTS.md`) rather than internal memory.
- The agent does **not** commit on its own: work stays uncommitted until the
  user validates it, including running the tests. Push only when the user asks.
- Commits, pull request descriptions, issues and comments carry no
  `Co-Authored-By` trailer, "Generated with ..." line or any other AI
  attribution: the author is the only author. This overrides the default
  attribution of the tooling.
- Leave the working tree in a buildable, testable state after each change.
- Prefer small, reviewable increments.
- This is a proof of concept: when an experiment fails or a risk is confirmed,
  record it honestly in the findings report instead of working around it.

## GitHub workflow (issues, branches, PRs)

Repository: `git@github.com:madialeva/vagalume.git`.

There is no `main`/`master` once the project is under way. The long-lived,
default branch is `develop/vX.Y.Z` (for example `develop/v0.1.0`), which holds
the version currently under development. Releasing cuts `release/vX.Y.Z` from
it and tags `vX.Y.Z`; hotfixes bump the patch digit. When a new cycle starts,
`develop/vX.Y.Z` is created from the released tag and becomes the default
branch. The program version lives as the single source of truth in
`Directory.Build.props` (`Version`) and must match the branch suffix. Until the
CI workflow exists, check it by hand when cutting a branch; the CI change adds
the automatic check on `develop/**` and `release/**` pushes.

Each OpenSpec change is tracked on GitHub with this cycle:

1. Planned changes may have a GitHub issue **before** their OpenSpec proposal
   exists (roadmap issue: scope and rationale, in English). The proposal is
   written when the change is picked up.
2. OpenSpec proposal approved by the user.
3. The change has a GitHub issue (create it if it does not exist yet) linking
   its `openspec/changes/` folder, assigned to the milestone of its target
   version (one milestone per version, named `vX.Y.Z`; the Projects board, if
   any, stays light: Todo / In progress / Done). While active, the change
   folder is named `is<n>-<slug>` after its issue; the date prefix is added
   only when the change is archived.
4. Branch created from the issue (Development panel → "Create a branch"; name
   like `change/is<n>-<slug>`) starting from `develop/vX.Y.Z`.
5. Implementation on the branch + push (pushes are done by the user). The agent
   never commits on its own: work stays uncommitted on the branch until the
   user validates it (including running the tests); commit only after the user
   explicitly confirms.
6. PR toward `develop/vX.Y.Z` with `Closes #<n>` in the description → the CI
   (once it exists) validates the PR → the user reviews the diff.
7. Squash merge as the norm (one change = one clean commit on the development
   branch). Exception: PRs whose intermediate commits have standalone value
   (e.g. massive deletions separated from new code) → normal merge.
8. The last commit on the branch may be the archiving of the change (only
   after user confirmation), so merged PR = closed issue (automatic via
   `Closes`) = archived change.
9. After archiving a change, always review its Non-Goals section. For each
   line leaving useful pending work, create a follow-up issue (English) that
   explains the pending scope, links the archived change, keeps the relation to
   the original Non-Goal, and carries the milestone of the version where it is
   planned. Skip Non-Goals already covered by an existing roadmap issue: link
   to that issue instead of duplicating it.

## Current status

- **Phase:** proof of concept.
- **Archived changes:** `is1-embedded-postgres-spike` (embedded PostgreSQL host;
  file-level backups because the minimal package has no `pg_dump`/`pg_restore`),
  `is6-blazor-electron-skeleton` (Blazor Server skeleton with web and Electron.NET
  desktop hosts) and `is15-wasm-ui-skeleton` (Blazor WebAssembly UI without Razor,
  REST API and WASM desktop host; works in desktop mode at a small cost over
  Blazor Server). All validated on Linux x64 only; Windows x64 is not validated
  and orphan processes in Electron.NET Core are an open risk: see the findings
  reports in `README.md`.
- **Active change:** none.
- **Repository state:** the default branch is `develop/v0.1.0`.
