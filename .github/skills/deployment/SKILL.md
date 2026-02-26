---
description: "Deployment infrastructure for Dawning.Agents: Docker, K8s, observability, rollback. Trigger: 部署, deploy, docker, k8s, kubernetes, 容器, container, helm, rollback, 回滚, observability, 可观测性, grafana, prometheus"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} deployment — {触发原因}`

# Deployment Infrastructure

## Docker

### Build Image

```bash
docker build -f deploy/docker/Dockerfile -t dawning-agents:latest .
```

Multi-stage build: `sdk:10.0` (build) → `aspnet:10.0-alpine` (runtime). Publishes `Dawning.Agents.Core.dll`. Exposes port 8080. Health check at `/health/live`.

### Development (`docker-compose.dev.yml`)

```bash
docker compose -f docker-compose.dev.yml up -d
```

Services:
- **dawning-agents** — builds from source, mounts `./:/src:ro`, port 8080
- **redis** — Redis 7.2-alpine, port 6379
- **postgres** — PostgreSQL 16-alpine, port 5432 (user: postgres, db: agents)

### Production (`docker-compose.prod.yml`)

```bash
docker compose -f docker-compose.prod.yml up -d
```

Uses pre-built image `changjianwang/dawning-agents:latest`, exposes port 80→8080. Same Redis + PostgreSQL stack.

### Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Development | Environment name |
| `Redis__ConnectionString` | `redis:6379` | Redis connection |
| `PostgreSQL__ConnectionString` | — | PostgreSQL connection |

## Kubernetes (`deploy/k8s/`)

### Deploy

```bash
cd deploy/k8s && bash deploy.sh
```

Applies in order: namespace → redis → configmap + secret → deployment + service → HPA → ingress.

### Manifests

| File | Purpose |
|------|---------|
| `namespace.yaml` | Creates `dawning-agents` namespace |
| `redis.yaml` | Redis StatefulSet |
| `configmap.yaml` | Application configuration |
| `secret.yaml` | Sensitive values (base64-encoded) |
| `deployment.yaml` | Main app deployment |
| `service.yaml` | ClusterIP service |
| `hpa.yaml` | Auto-scaling (2–10 replicas) |
| `ingress.yaml` | External routing |

### HPA Configuration

- CPU target: 70% utilization
- Memory target: 80% utilization
- Scale-up: max 100% or 4 pods per 15s
- Scale-down: stabilization 300s, max 10% per 60s

### Rollback

```bash
# View history
kubectl rollout history deployment/dawning-agents -n dawning-agents

# Rollback to previous
kubectl rollout undo deployment/dawning-agents -n dawning-agents

# Rollback to specific revision
kubectl rollout undo deployment/dawning-agents -n dawning-agents --to-revision=2

# Verify
kubectl rollout status deployment/dawning-agents -n dawning-agents
```

## Observability Stack (`deploy/observability/`)

```bash
cd deploy/observability && docker compose up -d
```

| Service | Port | Purpose |
|---------|------|---------|
| Prometheus | 9090 | Metrics collection |
| Grafana | 3000 | Dashboards (admin/admin) |
| Loki | 3100 | Log aggregation |
| Tempo | — | Distributed tracing |
| OTEL Collector | 4317/4318 | OpenTelemetry ingest (gRPC/HTTP) |

### Grafana Setup

Pre-provisioned datasources (Prometheus, Loki, Tempo) and dashboards under `deploy/observability/grafana/`.

### Application Integration

Register in the app with DI:

```csharp
services.AddOpenTelemetryObservability(config);  // from Dawning.Agents.OpenTelemetry
services.AddAgentTracing();
services.AddAgentMetrics();
```

Configure OTEL endpoint in `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "Endpoint": "http://otel-collector:4317"
  }
}
```

## Health Checks

The Dockerfile configures a health check hitting `/health/live` every 30s. In K8s, liveness and readiness probes should target the same endpoint.

Verify locally:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```
