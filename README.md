# Vagalume

A proof of concept for running **one .NET application in two modes with the same
code**: as an autonomous desktop application, and as a web service hosted by a
company for several users.

Vagalume grew out of [Arume](https://github.com/angazo/arume), a billing and
accounting product written in Java, and may end up as a project detached from
it. Nothing here is production-ready: the goal is to gather evidence before
deciding anything.

> **Status:** early exploration. The first change (embedded PostgreSQL host) is
> implemented and validated on Linux x64; Windows x64 is not validated yet.

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

Other directions are under evaluation but not decided: Blazor Server as the UI
shared by both modes, Electron.NET or Photino as desktop shell, and EF Core
against PostgreSQL only.

## Platforms

Linux x64 and Windows x64. macOS and ARM are out of scope.

## Requirements

- **.NET 10 SDK**.
- `tar` with xz support (included in Linux and in Windows 10 or later).
- Internet access the first time you build, to download the PostgreSQL binaries
  (they are never downloaded at runtime).

## Development

Layout:

| Path                                      | Contents                                                              |
| ----------------------------------------- | --------------------------------------------------------------------- |
| `src/Vagalume.Postgres.Embedded`          | The embedded PostgreSQL host library                                  |
| `tests/Vagalume.Postgres.Embedded.Tests`  | Unit tests and integration tests (they start a real PostgreSQL)       |
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

PostgreSQL refuses to run as `root`; the host fails early with a clear error.

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

## Design record

The design of every change (proposal, design, specifications and tasks) is kept
in [`openspec/`](openspec/), written in Spanish as a record of how the project
is developed with the help of AI agents. Agents working on the repository should
read [`AGENTS.md`](AGENTS.md) first.

## License

Released under the **MIT License**. You are free to use, modify, and distribute
this software, including in proprietary projects, provided you retain the
original copyright notice. See [LICENSE.md](LICENSE.md) for details.
