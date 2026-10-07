namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Where the PostgreSQL distribution lives, where the cluster is stored and which role and database the host manages.
/// </summary>
/// <param name="BinariesDirectory">Root of the extracted distribution of the running platform (contains <c>bin</c>).</param>
/// <param name="DataDirectory">Directory of the cluster; created on first start.</param>
public sealed record EmbeddedPostgresOptions(
    string BinariesDirectory,
    string DataDirectory,
    string UserName = "vagalume",
    string DatabaseName = "vagalume",
    string IcuLocale = "und");
