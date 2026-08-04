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

## Migrations and Seeding

Migrations live in `TodoDemo/Database/Migrations` (use `--output-dir Database/Migrations` when generating new ones).

The app does not apply migrations automatically at startup.
Instead, `dotnet TodoDemo.dll --migrate` runs the migrations then exits.
This is the entrypoint of the one-shot `migrate` service in `compose.yml`.

The `seed.sh` script can be used to populate the DB with example data through the API.
```bash
chmod +x seed.sh && ./seed.sh 
```
On non-UNIX systems, the script can be run through a container.
```bash
docker run --rm --network host -v "$PWD/seed.sh:/seed.sh:ro" \
    buildpack-deps:curl bash /seed.sh
```

## Deploying to Scaleway

> **Note:** everything from here on out is work in progress!

1. Create a PostgeSQL database with the desired settings.
For this demo, the smallest instance with 5 GB storage is plenty.

Verify connection from local machine

TODO: Connect with TSL certificate verification. The certificate can be downloaded from the Scaleway console.

2. Attach private network to the database
note the IP configuration

Restrict access to the private network only by removing the `0.0.0.0/0` rule from the allowed IPs.

2. Create a container registry or use an existing one
- Sign into the registry on local machine using existing or new API token

3. Build and push image to container registry
```
docker compose build
docker tag todo-demo:local rg.fr-par.scw.cloud/robin-todo-demo-cr/todo-demo:0.1.0
docker push rg.fr-par.scw.cloud/robin-todo-demo-cr/todo-demo:0.1.0
```
Use immutable version tags (0.1.0, 0.1.1), not latest. Scaleway recommends this explicitly, and it's what makes a rollback a matter of pointing the container at the previous tag.

4. Create Serverless Container
Create a new Container in an existing or new namespace.

Resources
- Memory: 1024 MB
- vCPU: 1000 mvCPU (1 vCPU)
More vCPU during startup measurably reduces cold start, and .NET's JIT is startup-heavy. You can dial it down after you've measured.

Scaling
- Min scale: 0 for now (switch to 1 in §22 if cold starts annoy you)
- Max scale: 3
- Scaling policy: concurrent requests, threshold 20

Advanced
- Sandbox: v2
- Request timeout: 60s
- Privacy: Public

TODO: fine-tune scaling parameters based on application load data

Environment variables
- ASPNETCORE_ENVIRONMENT=Production
- ...other application config

Secrets
- ConnectionStrings__TodoDb="Host=<endpoint-host>;Port=<port>;Database=<database>;Username=<user>;Password=<password>;SSL Mode=REquire;Maximum Pool Size=10"
TODO: SSL verification
TODO: tune maximum pool size based on scaleway manages PostgreSQL instance

Attach to the same private network as the database. Make sure to use the private IP of the database in the connection string.

Configure health check to use the `/health/ready` HTTP endpoint.
