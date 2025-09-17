This project implements a set of small, independently deployable microservices with strict swimlane fault isolation. Each service owns its database and communicates only via service-toservice HTTP. Article data is sharded per continent (Y-axis spilt), while comments and profanity are single-lane services.

The Artchiecture (Containers and responsibilites)
ArticleService - REST API
It's purpose is CRUD operations + paginated reads for articles.
It storages one SQL Servcer instance per swimlane: Global plus one per continent(Africa, Antarctica, Asia, Europe, NorthAmerica, Australia, SouthAmerica)
The caller can target a continent, if there is no target the fallback option is to the global Shard.

At start up the ShardMigratorHostedService migrates all shards at boot with retries.
ConfigurationConnectionString resolves connection strings for each shard key at runtime.

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

