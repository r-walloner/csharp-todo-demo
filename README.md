# TodoDemo

A simple C# ASP.NET todo app with EF Core, PostgreSQL and a plain JS web UI.
It exists to try out .NET on Scaleway Serverless Containers.

## Running locally

```bash
docker compose up --build --detach
```

This starts Postgres, runs migrations in a one-shot container, then the app. 
- UI and API are served by the same container on http://localhost:8080.
- API docs are available at http://localhost:8080/scalar
- DB admin interface is available on http://localhost:8082.

To run the app directly through the SDK instead, Postgres needs to be reachable on `localhost:5432` with the credentials from `TodoDemo/appsettings.Development.json` (the compose `db` service publishes that port):

```bash
docker compose up db --detach
dotnet run --project TodoDemo
```

## Migrations

Migrations live in `TodoDemo/Database/Migrations` (use `--output-dir Database/Migrations` when generating new ones).

The app does not apply migrations automatically at startup.
Instead, `dotnet TodoDemo.dll --migrate` runs the migrations then exits.
This is the entrypoint of the one-shot `migrate` service in `compose.yml`.

