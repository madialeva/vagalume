# Vagalume

A proof of concept for running **one .NET application in two modes with the same
code**: as an autonomous desktop application, and as a web service hosted by a
company for several users.

Vagalume grew out of [Arume](https://github.com/angazo/arume), a billing and
accounting product written in Java, and may end up as a project detached from
it. Nothing here is production-ready: the goal is to gather evidence before
deciding anything.

> **Status:** early exploration. The embedded PostgreSQL host, the Blazor Server
> skeleton and the Blazor WebAssembly alternative (both with an Electron.NET
> desktop host) are implemented and validated on Linux x64; Windows x64 is not
> validated yet.

---

## The question behind it

If the same application must run on a single user's computer and on a company
server, the database is the first fork in the road. Using a different engine in
each mode (for example SQLite locally and PostgreSQL on the server) means two
SQL dialects, two sets of migrations and tests against both, with differences an
ORM does not fully hide (decimal types, concurrency tokens, collations).

The alternative explored here is **PostgreSQL in both modes**. On the server it
is a regular installation. In the autonomous mode it is **bundled with the
application** and started as a child process listening only on `127.0.0.1`, so
the user installs nothing and the developer maintains one engine, one dialect
and one migration chain.

"Embedded PostgreSQL" is not a library like SQLite: it is a real server with
its own lifecycle. This proof of concept checks, with automated tests, whether
that is manageable: what the minimal binary package really contains, whether it
recovers from a crash, whether access is truly restricted to the local user and
whether it behaves the same on Linux and Windows.

## Roadmap

Work is tracked as GitHub issues. The table shows what is planned.

| Status | Feature                                                                                                           | Target | Issue |
| :----: | ----------------------------------------------------------------------------------------------------------------- | :----: | :---: |
|   ✅   | Embedded PostgreSQL host: pinned and verified binaries, restricted access, crash recovery, backup and restore     | v0.1.0 |  #1   |
|   ✅   | Blazor Server skeleton: layered solution, EF Core, web host, Electron.NET desktop host and packaging              | v0.1.0 |  #6   |
|   ⬜   | Blazor WebAssembly UI without Razor, REST API and Electron.NET desktop host                                       | v0.1.0 |  #15  |

## Platforms

Linux x64 and Windows x64. macOS and ARM are out of scope.

## Requirements

- **.NET 10 SDK**.
- **Node.js 22 or later**, only to build and run the desktop host.
- `tar` with xz support (included in Linux and in Windows 10 or later).
- Internet access the first time you build, to download the PostgreSQL binaries
  (they are never downloaded at runtime).

## Development

Layout:

| Path                                      | Contents                                                              |
| ----------------------------------------- | --------------------------------------------------------------------- |
| `src/Vagalume.Core`                       | Domain and use cases (plain .NET, depends on nothing)                 |
| `src/Vagalume.Data`                       | EF Core persistence on PostgreSQL and the single migration chain      |
| `src/Vagalume.UI`                         | Blazor Razor Class Library shared by both hosts                       |
| `src/Vagalume.Host.Web`                   | ASP.NET Core host for a server, with an external PostgreSQL           |
| `src/Vagalume.Host.Desktop`               | The same application in an Electron.NET window, with embedded PostgreSQL |
| `src/Vagalume.Postgres.Embedded`          | The embedded PostgreSQL host library                                  |
| `tests/`                                  | One test project per source project, plus `Vagalume.Testing` (shared PostgreSQL test environment) and `Vagalume.Architecture.Tests` (layer rules) |
| `eng/`                                    | Binaries lock file and the file-based .NET script that fetches them   |
| `artifacts/postgres/<platform>/`          | Downloaded binaries (git-ignored, created by the build)               |

```bash
dotnet build                              # also fetches the PostgreSQL binaries of this platform
dotnet test                               # unit + integration tests
dotnet test --filter Category=Integration # only the tests that start PostgreSQL
dotnet format --verify-no-changes         # formatting and analyzers
```

The build runs `eng/fetch-postgres-binaries.cs` (a .NET 10 file-based app, so
it behaves the same on Linux and Windows). It downloads the pinned artifact
from Maven Central, verifies its SHA-256 against `eng/postgres-binaries.lock`,
and extracts it. It does nothing while the lock file is unchanged. The
extraction shells out to `tar`, because .NET has no xz support and no NuGet
package may be added; `tar` ships with Linux and with Windows 10 or later.

Usage from code:

```csharp
var host = EmbeddedPostgresHost.Create(new EmbeddedPostgresOptions(binariesDirectory, dataDirectory));
await host.StartAsync(cancellationToken);   // initdb on first run, reuses a running server
await using var connection = new NpgsqlConnection(host.ConnectionString);
// ...
await host.StopAsync(cancellationToken);
```

### Web host

`Vagalume.Host.Web` serves the shared Blazor Server UI and talks to an
**external** PostgreSQL; it never starts an embedded one. The connection string
comes from the configuration key `ConnectionStrings:Vagalume`, for example with
an environment variable:

```bash
ConnectionStrings__Vagalume="Host=db;Port=5432;Username=vagalume;Password=...;Database=vagalume" \
ASPNETCORE_URLS=http://127.0.0.1:5021 \
dotnet run --project src/Vagalume.Host.Web
```

The host applies the database migrations on startup. Without the key it exits
with a message naming it; if the database cannot be reached it exits saying so.

> **The web host has no authentication yet.** Anyone who can reach it can read
> and change the data, so do not expose it to the internet as it is. Users and
> roles are tracked as a separate issue.

### Desktop host

`Vagalume.Host.Desktop` is the same application inside an Electron.NET Core
window with its own embedded PostgreSQL. It needs **Node.js 22 or later** at
build time and a display at run time:

```bash
dotnet run --project src/Vagalume.Host.Desktop
```

On start it creates the user's cluster (under the per-user data directory,
`$XDG_DATA_HOME/Vagalume/data` on Linux), applies the migrations, serves the UI
on a random `127.0.0.1` port and opens the window. Closing the window stops the
web server and then PostgreSQL. The first build downloads Electron and the
ASP.NET Core web assets once; the application never downloads anything while
running.

The local port is protected: every start generates a random token, the window
exchanges it for an `HttpOnly` cookie, and any other request gets `403`. The
token is kept out of the logs.

#### Packaging the desktop application

A package must be built on its own platform (Electron.NET Core refuses to
cross-build). On Linux x64:

```bash
dotnet publish src/Vagalume.Host.Desktop -c Release -r linux-x64 --self-contained
```

The result is `src/Vagalume.Host.Desktop/bin/Release/net10.0/linux-x64/publish/vagalume-host-desktop-x64-0.1.0.tar.xz`
(about 133 MB; the unpacked application is about 524 MB). It contains Electron,
the self-contained .NET application and, under `resources/bin/postgres`, only
the PostgreSQL distribution of that platform. Extract it anywhere and run
`Vagalume.Host.Desktop`; it needs no .NET, no Node.js and no network.

On Windows x64 the same command with `-r win-x64` is expected to produce a
portable executable, but **it has not been built or run** (see the findings).

PostgreSQL refuses to run as `root`; the host fails early with a clear error.

### WebAssembly alternative

A second UI architecture lives next to the Blazor Server one: the interface runs
as **Blazor WebAssembly**, is written in C# with **no Razor** (components are
classes that return a tree built with a small markup DSL), and talks to the
server only through a **REST API**, so it never sees the database.

| Path                                   | Contents                                                              |
| -------------------------------------- | --------------------------------------------------------------------- |
| `src/Vagalume.Api.Contracts`           | DTOs, routes, `INotesApi` and the client-side exceptions; depends on nothing |
| `src/Vagalume.Api`                     | Minimal APIs over `Core`, errors as `ProblemDetails`                  |
| `src/Vagalume.Api.Client`              | `HttpClient` implementation of `INotesApi`                            |
| `src/Vagalume.Wasm.UI`                 | The WebAssembly UI and its markup DSL (no `.razor` files)             |
| `src/Vagalume.Desktop.Hosting`         | Session token, local-port guard and PostgreSQL start/stop shared by both desktop hosts |
| `src/Vagalume.Host.Wasm.Desktop`       | Electron window serving the WASM app and the API from one local origin |

```bash
dotnet run --project src/Vagalume.Host.Wasm.Desktop
dotnet publish src/Vagalume.Host.Wasm.Desktop -c Release -r linux-x64 --self-contained   # package, build on the target OS
```

It needs the same prerequisites as the other desktop host (Node.js 22 or later
and a display). The first request of the window carries the session token, which
is exchanged for a cookie; that cookie then also accompanies the download of the
WebAssembly files and every API call, because everything shares one origin.

## Findings report (embedded PostgreSQL spike)

Results of the first change, measured with PostgreSQL **18.6.0** (Zonky
`embedded-postgres-binaries`) on Linux x64.

### What the minimal package contains

- **Executables:** only `initdb`, `pg_ctl` and `postgres` on both platforms.
  There is **no `pg_dump`, `pg_restore`, `psql` or `pg_basebackup`**.
- **Libraries:** `libpq`, ICU 60 (Linux) / ICU 77 (Windows), OpenSSL, zlib,
  libxml2, libxslt, liblzma. Linux binaries find them through `RPATH`, so no
  system library besides libc is needed.
- **Extensions:** the standard `contrib` set (for example `pg_trgm`, `citext`,
  `pgcrypto`, `hstore`, `btree_gin`, `pg_stat_statements`).
- **Collations:** `initdb` accepts the ICU provider (`und` locale is used, so
  ordering does not depend on the operating system locales) and the built-in
  `C.UTF-8`. Without options it falls back to the machine locale, which is
  not reproducible across computers, so the host always passes encoding and
  locale explicitly (`UTF8`, ICU `und`, libc `C`).

### Size and timing (Linux x64, local SSD)

| Measure                                        | Result     |
| ---------------------------------------------- | ---------- |
| Distribution on disk, Linux                    | ~57 MiB    |
| Distribution on disk, Windows                  | ~109 MiB   |
| Data directory after `initdb`                  | ~47 MiB    |
| First start (`initdb` + start + create database) | ~1.2 s   |
| Start on an existing cluster                   | ~0.13 s    |

### Test results

| Risk                                                  | Linux x64 | Windows x64    |
| ----------------------------------------------------- | :-------: | :------------: |
| Full cycle: start, Npgsql, write, read, clean stop    | passes    | **not tested** |
| Restart keeps data and does not run `initdb` again    | passes    | **not tested** |
| Restricted access (no password, wrong password, loopback only, no Unix socket, no `trust`, secret file `0600`) | passes | **not tested** |
| `SIGKILL` of the server, restart, committed data intact, stale `postmaster.pid`, no second server | passes | **not tested** |
| Second host reuses the running server                 | passes    | **not tested** |
| Backup and restore into a new directory               | passes    | **not tested** |
| Tampered hash in the lock file fails the fetch        | passes    | **not tested** |
| Works offline with binaries already extracted         | passes (*)| **not tested** |
| `root` is rejected with a clear error                 | unit test | n/a            |

(*) Verified by running the whole suite in a network namespace with only the
loopback interface. Every test passes except the tampered-hash one, which by
nature must download the artifact before it can detect the manipulation.

### Windows x64 is not validated

No Windows machine or runner was available during this change, so every
Windows behavior is **unverified**: the `.exe` binaries, the `tar` extraction,
forced termination, the ACL of the secret file (the host relies on the user
profile ACL being inherited and does not set it explicitly), the behavior of
`initdb --auth-local` on Windows, and whether the Visual C++ runtime is needed
on a clean installation. It must be checked, ideally in CI, before relying on
this approach. The lock file does list the Windows artifact and the fetch
script extracts it correctly (checked from Linux).

### Backup limitation

Without `pg_dump` and `pg_restore` the host backs up at file level: it stops
the server, copies the data directory and starts the server again. This is
consistent and needs no extra tools, but it stops the server, only restores on
the same PostgreSQL major version and platform, and cannot be used to migrate
between major versions. A logical dump would require the official PostgreSQL
binaries (a second pinned source and much more size).

### Open risks

- **Major-version upgrade** is not implemented: a file-level backup cannot cross
  major versions and there is no `pg_upgrade` in the package. It remains the
  largest long-term maintenance cost of this approach.
- **Secret storage** is a file with owner-only permissions next to the data
  directory (it lives inside it, so backups carry it too). A product should use
  the operating system facility (DPAPI, keyring).
- **Orphan servers:** the server outlives the host by design (`pg_ctl`). That
  is what allows reuse and recovery, but an application needs a policy for
  stopping it on exit.
- **Concurrent hosts:** two hosts starting at exactly the same time on the same
  data directory are not guarded against.
- **Package provenance:** the binaries come from a third-party build (Zonky) of
  PostgreSQL; ICU 60 and OpenSSL 1.1 in the Linux build are old and will not
  receive security updates from that build line.

### Recommendation

For a **desktop mode with one local user**, embedding PostgreSQL is viable on
Linux: it starts in about a second, recovers from a forced kill without data
loss, is reachable only through an authenticated loopback connection, and keeps
one SQL dialect between both modes. It is **not yet proven for production**:
Windows is untested, there is no logical backup or major-version upgrade path,
and the bundled Linux libraries are old. Before adopting it, validate Windows
x64 in CI, decide how a product would upgrade across PostgreSQL major versions
and how it will store the secret, and consider the official PostgreSQL
binaries if logical backups are required.

### License notice

PostgreSQL is released under the PostgreSQL License. Redistributing its
binaries with an application requires keeping this notice (copy the exact
year range from the `COPYRIGHT` file of the PostgreSQL release being shipped;
the text below follows the PostgreSQL License):

> PostgreSQL Database Management System (formerly known as Postgres, then as
> Postgres95)
>
> Portions Copyright © 1996-2026, The PostgreSQL Global Development Group
>
> Portions Copyright © 1994, The Regents of the University of California
>
> Permission to use, copy, modify, and distribute this software and its
> documentation for any purpose, without fee, and without a written agreement
> is hereby granted, provided that the above copyright notice and this
> paragraph and the following two paragraphs appear in all copies.
>
> IN NO EVENT SHALL THE UNIVERSITY OF CALIFORNIA BE LIABLE TO ANY PARTY FOR
> DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, INCLUDING
> LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION,
> EVEN IF THE UNIVERSITY OF CALIFORNIA HAS BEEN ADVISED OF THE POSSIBILITY OF
> SUCH DAMAGE.
>
> THE UNIVERSITY OF CALIFORNIA SPECIFICALLY DISCLAIMS ANY WARRANTIES,
> INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
> FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS ON AN
> "AS IS" BASIS, AND THE UNIVERSITY OF CALIFORNIA HAS NO OBLIGATIONS TO PROVIDE
> MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.

The distribution also bundles third-party libraries (ICU, OpenSSL, zlib,
libxml2, libxslt, liblzma) with their own licenses, to be reviewed before
shipping a product.

## Findings report (Blazor Server skeleton with Electron.NET Core)

### Electron.NET Core behavior (verified with a throwaway project)

Observed with `ElectronNET.Core` 0.6.0, .NET 10, Node.js 24.17, on Linux x64
under a virtual X server (`xvfb-run`). The project is a pre-1.0 package, so
all of this may change.

- **How it starts.** Unpackaged (`dotnet run`), .NET starts first and launches
  Electron as its child process; Electron connects back through a Socket.IO
  server that listens on `127.0.0.1` with a random port and a random key. In a
  packaged app the order is expected to be reversed (Electron first); that is
  checked in the packaging group. ASP.NET Core listens on `127.0.0.1` with a
  random port chosen by Electron.NET, not by the application.
- **The window address does not travel on the command line.** Electron's
  arguments are `main.js -unpackeddotnet --trace-warnings -electronforcedport=0
  <path to the .NET app>`. The page to load is sent over the IPC socket, so a
  session token inside the URL is not visible in the process list.
- **Closing the window shuts everything down cleanly:** `ApplicationStopping`
  and `ApplicationStopped` fire and no process is left behind. Killing the
  Electron main process also makes the .NET host stop gracefully.
- **Killing the .NET process (`kill -9`) leaves Electron running as an
  orphan** (observed for at least 60 s). Because `ElectronSingleInstance`
  defaults to `true`, **the next launch of the application exits immediately**
  while the orphan holds the single-instance lock. This is the main weakness
  found.
- **Defaults to watch.** The package defaults to Electron 30.4.0, which is out
  of support, and its `install.js` silently fails to extract under Node 24
  (the binary never appears). Setting `ElectronVersion` (44.5.1 was used) fixes
  it, because newer versions are installed with an explicit step the targets
  run. The build runs `npm install` and downloads Electron at **build** time
  only. A user-level npm setting such as `allow-scripts` can block Electron's
  install script, and an `ELECTRON_RUN_AS_NODE=1` variable in the environment
  (it is set by some editors) stops Electron from starting as an application.
- **Packaging rules.** A `Properties/electron-builder.json` is required. The
  default Linux target is `tar.xz` with `--no-sandbox` as an executable
  argument, so the Chromium sandbox is off by default. **A target platform
  must be built on that platform** (a Windows package cannot be built on
  Linux; the tooling stops with error `ELECTRON100`).
- **Size.** The unpacked Electron distribution for Linux is about 283 MB.

### Desktop host findings

- **The Blazor client script was missing at first.** The Web SDK only includes
  `_framework/blazor.web.js` when the host project itself contains `.razor`
  files; here they live in the shared UI library, so both hosts returned `404`
  for the script and the page rendered but was never interactive. Setting
  `RequiresAspNetWebAssets` to `true` in both host projects fixes it, and tests
  now check that the script is served.
- **The request log wrote the token.** ASP.NET Core logs the full URL of every
  request, including the query string where the one-time token travels. The
  desktop host raises that logger to `Warning`, and a test asserts that the
  token never appears in any log message.
- **Forced kill of the .NET host (`kill -9`)** leaves Electron running and
  PostgreSQL up. Launching the application again starts a host that stops at
  once, because Electron's single-instance lock is still held, and that host
  also stops the reused PostgreSQL. The user has to close the orphaned window
  before the application starts again. Forced kill of PostgreSQL itself is
  recovered on the next launch with no loss of committed notes (automated test).
- **Closing the window** (here the Electron main process was asked to quit)
  ends everything cleanly: no host, Electron or PostgreSQL process remains and
  `postmaster.pid` is removed.
- **The interactive channel works in the real window.** In an Electron window
  on a virtual display, clicking *Añadir* with an empty text shows the
  validation message through the Blazor connection. Keyboard input could not
  be injected without a window manager, so note creation through typing was
  verified through the services, not through the window.
- **Copying the PostgreSQL distribution into the build output follows
  symbolic links**, so the copy takes about 123 MB instead of the 60 MB of the
  extracted archive.

### Packaging findings (Linux x64)

- **Launch order changes when packaged.** Unpackaged, .NET starts Electron;
  in the package **Electron starts the .NET application** (`PackagedElectronFirst`).
- **Strict run of the package.** Extracted from the `.tar.xz` and run as a
  regular user, with no network (a network namespace with only loopback) and an
  empty `PATH` (no `dotnet`, no `node`): it creates its cluster, shows the notes
  page and, when asked to quit, leaves no host, Electron or PostgreSQL process
  and removes `postmaster.pid`.
- **Start-up times** (virtual display, local SSD): cold start, which runs
  `initdb`, has PostgreSQL up after about 1.1 s and the window drawn after about
  3.3 s; a warm start takes about 0.55 s and 1.9 s.
- **Forced kill in the package.** Killing the .NET host leaves Electron and
  PostgreSQL running; launching the application again does not start a new
  host (Electron's single-instance lock is held) and, once the orphaned Electron
  is gone, PostgreSQL is still running. Killing Electron leaves the host running.
  The orphan problem exists in both directions and in both launch modes.
- **Electron terminated by `SIGTERM` aborted with `SIGTRAP`** (a core dump) in the
  strict run, although every other process was cleaned up. Closing the window
  with the mouse could not be automated without a window manager, so the normal
  close path was checked by quitting the application, not by clicking the close
  button.
- **The default `--no-sandbox` argument** of the Linux target in
  `electron-builder.json` only applies to packages that use a launcher script
  (AppImage, deb); the `tar.xz` runs the Electron binary directly and relied on
  unprivileged user namespaces for Chromium's sandbox on this machine.
- **Windows x64 is not built.** `dotnet publish -r win-x64` on Linux stops with
  `ELECTRON100`. The Windows target (`portable`, x64) is configured, and the
  build picks the PostgreSQL distribution from the operating system it runs on,
  so a Windows build would carry the Windows binaries, but none of this has run.
- **The PostgreSQL copy is larger than the archive** (about 123 MB in the
  package) because symbolic links in the Linux distribution are copied as files.

### Test results

| Check                                                                                 | Linux x64             | Windows x64    |
| ------------------------------------------------------------------------------------- | :-------------------: | :------------: |
| Layer rules (`Core` knows nothing, `Data` and `UI` only `Core`, hosts compose)         | passes (2 tests)      | **not tested** |
| Use cases and validation, conflict on a stale version                                  | passes (8 tests)      | **not tested** |
| EF Core on real PostgreSQL: migrate, persist, restart, repeated migration, `xmin`      | passes (5 tests)      | **not tested** |
| Web host: starts, serves the page and the Blazor script, persists, two browsers, missing key, unreachable database | passes (4 tests) | **not tested** |
| Desktop host: first start, second start keeps notes, stops PostgreSQL, rejects `root`, recovers from a killed PostgreSQL | passes (5 tests) | **not tested** |
| Session token: no token, wrong token, cookie exchange, interactive channel, foreign `Host`, new token per start, never logged | passes (5 tests) | **not tested** |
| Real Electron window (virtual display): page loads, interactive channel answers, clean quit | verified by hand  | **not tested** |
| Packaged `.tar.xz`: strict run (no network, not root, empty `PATH`), cold and warm start, clean quit | verified by hand | **not built** |
| Typing a note in the real window                                                       | **not verified** (no keyboard input without a window manager) | **not tested** |

All the automated tests start a real PostgreSQL. Clicking in the window and
reading its pixels used `xdotool` and a virtual X server; they are a manual
verification, not part of the test suite.

### What could not be validated

- **Windows x64**, entirely: the `.exe` binaries, building the package (which
  must happen on Windows), the window, the session token behavior and the
  packaged application. Issue #2 covers it.
- **Closing the window with the mouse**, and **typing a note** in the window, in
  the packaged and unpackaged runs; both need a window manager or a real
  desktop.
- **Behavior over time**: no long-running or multi-user test of the web host,
  and the interactive screens are only covered by their services and by hand,
  because there is no browser-driven test suite.

### Open risks

- **Orphaned processes** in both directions when one process is killed (see the
  desktop and packaging findings). A watchdog on one side, or a different
  single-instance policy, is needed before this is usable by non-technical
  people.
- **Electron.NET Core is a 0.x package**, defaults to an unsupported Electron
  version and has install-time sharp edges (Node version, npm script policy,
  environment variables). The Electron version has to be pinned and reviewed.
- **Weight**: 133 MB compressed and about 524 MB unpacked per platform, of which
  Electron is the largest part.
- **The web host has no authentication** and must not be exposed as it is.
- **The session token** does not protect against a process of the same user.
- **Packages must be built on each target operating system**, so a release
  needs a Windows build machine in addition to a Linux one.

### Recommendation

The architecture holds: one Blazor Server UI in a Razor Class Library, plain
`Core` and `Data` libraries, and two thin hosts run the same code in a server
and in a desktop window, with one PostgreSQL dialect and one migration chain.
That decision can be considered closed for the proof of concept.

Electron.NET Core is **workable on Linux but not ready to be adopted without
reservations**. It starts, shows the page, protects the local port with the
session token, shuts down cleanly and packages into a self-contained archive
that runs offline. Against that stand the orphan-process behavior, the weight of
the package, a 0.x package that needs careful pinning, and a Windows build that
is completely unverified. Before committing to it, validate Windows x64 in CI,
fix or design around the orphan processes, and compare it with a lighter shell
such as Photino.Blazor, which this change did not evaluate.

## Findings report (Blazor WebAssembly without Razor)

Results of the change that built the WebAssembly alternative, measured on
Linux x64 with the packaged applications run with no network, as a regular
user and with an empty `PATH`.

### Does it work?

**Yes, including in desktop mode.** The WebAssembly interface runs inside the
Electron window, loads its files and calls the REST API of the local server with
the session cookie of the window, and the database is never visible to it. In a
real window, a click on *Añadir* with an empty text produces a `400` from the
server and the interface shows its message. Nothing in the client references
`Core`, `Data`, EF Core or Npgsql: an architecture test checks the project
references and another one checks the files the browser downloads.

### Comparison with Blazor Server (same machine, same method)

| Measure                                          | Blazor Server | Blazor WebAssembly |
| ------------------------------------------------ | ------------: | -----------------: |
| Package (`.tar.xz`)                              |        133 MB |             145 MB |
| Unpacked application                             |        524 MB |             550 MB |
| Client files the browser downloads               | one script, ~200 KB | 8.8 MiB raw, 2.7 MiB Brotli, 3.4 MiB gzip |
| Cold start: window drawn / notes content drawn   | 3.4 s / 3.4 s |      3.1 s / 3.7 s |
| Warm start: window drawn / notes content drawn   | 1.7 s / 1.7 s |      1.7 s / 2.0 s |
| Memory after start (RSS sum, shared pages counted per process) | ~960 MiB | ~1010 MiB |
| of which Chromium renderer                       |       158 MiB |            229 MiB |
| of which .NET host                               |       179 MiB |            162 MiB |
| of which PostgreSQL                              |       226 MiB |            226 MiB |

Each figure comes from one run with a 0.25 s polling resolution, so differences
below about half a second are noise. The honest summary is that **WebAssembly
costs little more than Blazor Server in desktop**: about 12 MB more in the
package, about 0.3 to 0.5 s more until the notes appear, and about 70 MiB more
in the renderer, partly offset by a lighter .NET host. The 2.7 MiB of
compressed runtime is a local file here, so it costs nothing in bandwidth;
it would matter in the web mode.

### Writing the interface without Razor

The notes page is `NotesModel` (state and behavior, 93 lines), `NotesView` (the
view as a function of the model, 44 lines) and `NotesPage` (24 lines), on top of
a markup DSL of about 250 lines in one folder. The Blazor Server page, with
Razor, is one 110-line file.

```csharp
Form(
    Class("new-note"),
    OnSubmit(model.CreateAsync),
    Input(AriaLabel("Texto de la nota"), BindValue(model.NewText, v => model.NewText = v)),
    Button(Type("submit"), "Añadir")),
When(model.Message is not null, () => P(Class("message"), Role("alert"), model.Message!)),
```

- **What works well.** It is ordinary C#: the compiler and refactoring tools see
  every call, there is no second language to learn, and the view is a pure
  function, so the whole page is tested without a browser (7 tests of the page
  and 8 of the DSL and its emitter, 16 in all). Text is never interpreted as
  markup.
- **What is worse than Razor.** It is more code overall (the page is split in
  three files and the DSL is extra), plain HTML cannot be pasted in, and every
  attribute and element needs a helper. Event handlers and bindings are
  limited to what the DSL defines; a missing helper is written with `El(...)` and
  the generic attributes.
- **Verdict.** For a page this size the DSL is comfortable, and the separation
  between model, view and component is a clearer design than Razor's mixed
  file. Whether it scales to a whole product with many components is **not
  shown** by one page.

### Test results

| Check                                                                                   | Linux x64          | Windows x64    |
| --------------------------------------------------------------------------------------- | :----------------: | :------------: |
| API: create, list, update, validation 400, not found 404, conflict 409, no internals leaked | passes (8 tests) | **not tested** |
| HTTP client against the real API, errors mapped to the contract's exceptions            | passes (5 tests)   | **not tested** |
| DSL, emitter, notes page logic and view, client bundle contents                         | passes (16 tests)  | **not tested** |
| Layer rules, no `.razor` files in the WebAssembly UI                                    | passes (3 tests)   | **not tested** |
| WASM desktop host: session guard, boot files, API with the cookie, persistence, root, token not logged | passes (7 tests) | **not tested** |
| Shared desktop pieces keep the Blazor Server desktop host unchanged                     | passes (10 tests)  | **not tested** |
| Real Electron window: WASM boots, lists notes from the API, validation message from a click | verified by hand | **not tested** |
| Packaged `.tar.xz`: strict run, cold and warm start, clean quit                         | verified by hand   | **not built**  |
| Typing a note in the real window                                                        | **not verified** (no keyboard input without a window manager) | **not tested** |

### Findings worth knowing

- **The packaged app first showed a blank page.** The host copies `index.html`
  from the WebAssembly project as it is, so the fingerprint placeholder of the
  loader script (`blazor.webassembly#[.{fingerprint}].js`) was never replaced and
  the browser never asked for the WebAssembly files. Running with `dotnet run` hid
  it, because the static asset pipeline processes the file there. The fix was to
  disable fingerprinting of the boot files and write the literal script name; a
  test now checks the page. Packaged runs are the only place this showed up.
- **The boot files do carry the session cookie.** The worry that the browser
  might download the WebAssembly files without credentials did not materialize:
  one origin means one cookie for the page, the runtime and the API.
- **Unknown `/api` paths must not fall through to `index.html`**, or a typo in a
  call would return `200` with HTML; the host answers `404` for them.
- **Everything inherited from the Blazor Server skeleton still applies:** orphaned
  Electron and PostgreSQL processes after a forced kill, Windows not built or
  run, a `0.x` Electron.NET Core package and the weight of Chromium.

### What could not be validated

- **Windows x64**, entirely (issue #2).
- **Typing in the window.** Mouse clicks were automated, keyboard input was not.
- **The web mode with a remote server**, which was not built in this change: the
  API is independent of the host, but no web host for the WebAssembly UI exists
  yet, and the cost of the 2.7 MiB download over a real network was not measured.
- **Behavior at scale**: one page, one entity, no routing, no authentication.

### Recommendation

WebAssembly with a REST API and a Razor-free C# interface is **viable in desktop
mode** and costs only a little more than Blazor Server there, while giving the
clean separation the author wanted: a server with no UI logic, a client that can
never reach the database, and views that are plain, testable C#. It is
therefore a legitimate alternative, not a dead end.

It is not yet a replacement. The decision between the two depends on things this
change cannot settle: how well the home-made DSL holds up on a real product (a
component library or an established C# markup library may be worth a look), the
web mode where the download and the lack of a persistent connection matter in
opposite directions, and Windows. Before choosing, build one more realistic
screen with each approach and run the web mode of the WebAssembly variant.

## Design record

The design of every change (proposal, design, specifications and tasks) is kept
in [`openspec/`](openspec/), written in Spanish as a record of how the project
is developed with the help of AI agents. Agents working on the repository should
read [`AGENTS.md`](AGENTS.md) first.

## License

Released under the **MIT License**. You are free to use, modify, and distribute
this software, including in proprietary projects, provided you retain the
original copyright notice. See [LICENSE.md](LICENSE.md) for details.
