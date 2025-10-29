# HappyHeadliners — Microservices system

A .NET 8 microservices solution for publishing articles and managing comments with profanity filtering, immediate notifications, and daily newsletters. It emphasizes swimlane fault isolation (per-continent shards for articles), resilience, observability, and automation.

## Services

- ArticleService
  - CRUD and paginated reads for articles.
  - Per-continent sharding + Global shard, each with its own SQL database.
  - Consumes `PublishedArticle` events from RabbitMQ to persist articles.
  - Redis cache for item and recent lists, with metrics.

- PublisherService
  - Validates and publishes article requests to RabbitMQ (`PublishedArticle`).

- NewsletterService
  - Subscribes to `PublishedArticle` for “immediate” items (in-memory store).
  - Background worker for daily digest via HTTP to ArticleService.

- CommentService
  - Creates and lists comments for articles (single DB).
  - Calls ProfanityService via resilient HTTP (timeouts, retries, circuit breaker).
  - Fallback to local profanity dictionary if the service is down.
  - Optional Redis cache for per-article comments (LRU + metrics).

- ProfanityService
  - Profanity dictionary CRUD and a filter endpoint.

- DraftService
  - Snapshot drafts with content hashing to skip identical versions.

- Shared libraries
  - Shared.Messaging: RabbitMQ-based `IArticleQueue`.
  - Shared.Observability: Serilog + OpenTelemetry tracing/metrics, Prometheus exporter.

## Key flows

- Publish article
  - Client → PublisherService (HTTP) → RabbitMQ exchange → ArticleService subscriber → Persist to shard DB → Redis cache update.
- Comment on article
  - Client → CommentService (HTTP) → ArticleService (exists check) → ProfanityService (sanitize; fallback on failure) → Save comment to DB → Update comment cache if enabled.
- Newsletter
  - Event-driven immediate capture + scheduled daily digest via HTTP to ArticleService.

## Data, caching, and messaging

- Data
  - ArticleService: Global + per-continent SQL Server instances.
  - CommentService, ProfanityService, DraftService: one SQL Server each.
- Caching
  - ArticleService: `RedisArticleCache` with TTL and recent-IDs sorted set.
  - CommentService: `RedisCommentCache` with LRU eviction across articles and counters for hit/miss/evictions.
- Messaging
  - RabbitMQ fanout exchange `article.published` with resilient publisher/subscribers and trace propagation.

## Observability

- Serilog structured logging (compact JSON) + request logging with TraceId/SpanId.
- OpenTelemetry tracing: ASP.NET Core, HttpClient, SQL client.
- OpenTelemetry metrics: ASP.NET Core, HttpClient, .NET runtime, custom meters for caches.
- Prometheus scraping endpoint; Grafana dashboards included.

Files:
- `Shared.Observability/ObservabilityExtensions.cs`
- `observability/prometheus/prometheus.yml`
- `observability/grafana/**`

## Deployment and CI

- Docker multi-stage builds per service: `*/Dockerfile`.
- GitHub Actions publish versioned images to GHCR:
  - `.github/workflows/publish-images.yml` tags: `v*`, cleaned version, commit `sha`, and `latest`.
- `docker-compose.yml` orchestrates services, databases, RabbitMQ, Redis, and observability stack.

---

# Where AKF Principles are used (and why)

1. N + 1 Design — Partial
   - Per-continent article shards reduce SPOF vs. a single DB. More instances per service are expected at deploy/orchestrator level.
   - Evidence: README + `ArticleService.Infrastructure.ShardMigratorHostedService`.

2. Design for Rollback — Partial
   - Images are immutable and versioned (vX, sha) enabling app rollback. DB rollback is manual (no automated down migration).
   - Evidence: `.github/workflows/publish-images.yml`; EF migrations at startup in `Program.cs` and `ShardMigratorHostedService`.

3. Design to Be Disabled — Yes (feature toggles via config)
   - Observability can be disabled; comment cache can be disabled; migrations per shard can be narrowed; HTTPS redirect disabled in-container.
   - Evidence:
     - `Observability:Enabled` in `Shared.Observability/ObservabilityExtensions.cs`
     - `CommentCache:Enabled` in `CommentService/Program.cs`
     - `Migrations:EnabledShards` in `ArticleService/Infrastructure/ShardMigratorHostedService.cs`
     - HTTPS redirect condition in `CommentService/Program.cs`

4. Design to Be Monitored — Yes
   - Traces, metrics, logs, request logging, and Prometheus endpoints. Custom cache hit/miss/eviction metrics.
   - Evidence: `Shared.Observability/ObservabilityExtensions.cs`, `ArticleService/…RedisArticleCache.cs`, `CommentService/…RedisCommentCache.cs`, Grafana/Prometheus configs.

5. Design for Multiple Live Sites — Not evident
   - Architecture supports it (shards, messaging, stateless services); multi-region/site infra is not configured in code/CI.

6. Use Mature Technologies — Yes
   - .NET 8, EF Core, SQL Server, RabbitMQ, Redis, Serilog, OpenTelemetry, Prometheus, Docker, GitHub Actions.

7. Asynchronous Design — Yes
   - RabbitMQ decouples publisher from consumers; background workers handle periodic work.
   - Evidence: `Shared.Messaging/ArticleQueue`, `ArticleService/Messaging/ArticleQueueSubscriber.cs`, `NewsletterService/Messaging/ArticleQueueSubscriber.cs`, `NewsletterService/Services/DailyNewsletterWorker.cs`.

8. Stateless Systems — Yes
   - Services keep no in-memory session state; persistence is in SQL/Redis; background services isolated by DI scope.
   - Evidence: controllers/services across projects; caches in Redis only.

9. Scale Out, Not Up — Partial
   - Multiple small services, sharding, caches, and messaging all support horizontal scaling. Replica counts are set in runtime/orchestrator (not in repo).
   - Evidence: multi-service layout, Redis caches, RabbitMQ, per-continent shards.

10. Design for at Least Two Axes — Yes
   - X-axis: service decomposition. Z-axis: data sharding by continent (plus Global).
   - Evidence: solution layout; `ShardMigratorHostedService`.

11. Buy When Non-Core — Yes
   - Uses RabbitMQ, Redis, SQL Server, observability stack rather than custom implementations.

12. Commodity Hardware — Partial
   - Container-first and OSS stack. No explicit infra IaC here; assumption is commodity VMs/containers.

13. Build Small, Release Small, Fail Fast — Yes
   - Small services, independent images, resilient HTTP timeouts/retries, and circuit breaking. Startup migration with backoff.
   - Evidence: per-service Dockerfiles; `.github/workflows/publish-images.yml`; `CommentService/Program.cs` uses `AddStandardResilienceHandler()`; EF migration retry loops.

14. Isolate Faults — Yes
   - Swimlane isolation (own DB per service; article shards), HTTP resilience + fallback to local dictionary if ProfanityService is down, async queue prevents cascading sync failures.
   - Evidence: README; `CommentService/Program.cs`; `CommentService/Services/CommentService.cs` fallback; separate DB contexts; RabbitMQ.

15. Automation over People — Yes
   - CI builds and publishes images; DB migrations run automatically with retries; observability bootstraps itself.
   - Evidence: `.github/workflows/publish-images.yml`; EF `MigrateAsync()` loops in each service; `Shared.Observability`.

---

## Notable implementation details

- Resilience
  - Outbound HTTP: `AddStandardResilienceHandler()` (timeouts, retries, circuit breaker) for Profanity and Article existence clients.
  - EF Core: `EnableRetryOnFailure()` for transient SQL errors.
  - Startup DB migrations: exponential backoff on connection/migration failures.
  - RabbitMQ client: automatic connection/channel recovery, QoS/prefetch, nack with requeue, exponential backoff on subscribe failures.

- Caching metrics
  - `HappyHeadlines.Cache` meter; counters for hit/miss/evictions (comments) and hit/miss (articles) exported to Prometheus.

- Trace propagation
  - Producer/Consumer activities with OpenTelemetry `PropagationContext` via RabbitMQ headers.

---

## Run locally

- Build and run (compose or your preferred orchestrator)
  - Ensure SQL Server instances, RabbitMQ, and Redis are reachable (see `docker-compose.yml`).
- CI/CD
  - Push a tag like `v1.2.3` to automatically publish versioned images to GHCR.



<img width="1321" height="531" alt="Comment_Profanity_Services drawio" src="https://github.com/user-attachments/assets/e71e41f3-d2bb-449c-9809-9bf5720e16d3" />

<img width="1032" height="281" alt="DraftService drawio" src="https://github.com/user-attachments/assets/d664b72d-45a5-45c1-bb55-0d1cc17908cc" />

