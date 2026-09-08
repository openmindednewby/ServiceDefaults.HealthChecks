# Changelog

## 2.0.0 - BREAKING

- **Removed** `AddPostgresReadinessCheck` and `AddPostgresReadinessCheckFromConfiguration`.
  They moved to the new `Dloizides.HealthChecks.Npgsql` package, under the same
  `ServiceDefaults.HealthChecks` namespace — add the package reference and no code changes.
- **Removed** the `AspNetCore.HealthChecks.NpgSql` 8.0.2 PackageReference (and its transitive
  `Npgsql` 8.0.3). The package now has zero third-party dependencies: framework reference only.
  Consuming the `live`/`ready`/`start` tags and `MapHealthCheckEndpoints()` no longer drags a
  database driver into services that never open a Postgres connection.

### Migration

If you call either Postgres method, add:

```
dotnet add package Dloizides.HealthChecks.Npgsql
```

Nothing else changes. If you do not call them, upgrade to 2.0.0 and drop ~1.6 MB of assemblies.

## 1.3.1

- Previous release.
