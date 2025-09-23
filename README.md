This project implements a microservices system for publishing articles and managing comments, with a profanity dictionary service. It enforces strict swimlane fault isolation. Each service owns its database and services talk to each other with HTTP only. Articles are sharded per continent plus a global shard. Comments and profanity are single-lane services with single databases. 

Technology Stack: 
.Net 8 (ASP.NET Core + EF Core) 
SQL Server
Serilog (Logging)
OpenTelemetry (Tracing)
Docker Compose

The Artchiecture (Containers and responsibilites)
ArticleService - REST API
CRUD Operations and paginated reads for articles. Read can optionally include "Global" resuls when querying a specific continent.
One SQL Database per swimlane: Global, Africa, Antarctica, Asia, Europe, NorthAmerica, Australia, SouthAmerica.
It picks up a shard by Dependency-injected repository factory that selects Global or specific continent based on the request (Data/ArticleService.cs)
Mirgrations on startup. ShardMigratorHostedService connects to every configured shard and runs EF migrations with retries and logging progress (Infrastructure/ShardMigratorHostedService.cs)
It uses the shared library: Observability. 


CommentService - REST API
It's purpose is create and list comments for articles. 
It storages in a single SQL instance a dedicated CommentDatabase
It calles ProfanityService directly. 
It has the .Net 8 HTTP standard resilience using Polly with circuit breaker + retry + timeout.
It has fallback so if ProfanityService is down the LocalProfanityFilter sanitizes text. There is a background worker that refreshes its dictionary periodically.

ProfanityService - REST API
Its purpose is to maintain a profanity dicitonary and exposes a filter endpoint.
It stores in a single SQL instance, a dedicated ProfanityDatabase.
It has CRUD operations to create, read, update and delete profanitywords.


Deployment:
Docker compose orchestrates 8 SQL Server containers for Article shards, plus one each for ProfanityDatabase and CommentDatabase. Also 3 API containers.
It has Healthchecks to guard SQL Readiness. APIs expose HTTP only (For now, need to fix.)


<img width="1321" height="531" alt="Comment_Profanity_Services drawio" src="https://github.com/user-attachments/assets/e71e41f3-d2bb-449c-9809-9bf5720e16d3" />

<img width="1032" height="281" alt="DraftService drawio" src="https://github.com/user-attachments/assets/d664b72d-45a5-45c1-bb55-0d1cc17908cc" />

