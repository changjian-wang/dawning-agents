---
name: deployment
description: |
  Use when: Creating or modifying Docker configs, K8s manifests, observability stack (Prometheus/Grafana/Loki/Tempo), health checks, or rollback procedures
  Don't use when:
    - Building the project (use build-project)
    - Running tests (use run-tests)
    - Writing application code (use code-update)
    - Updating dependencies (use dependency-update)
    - Writing documentation (use markdown)
  Inputs: Deployment requirement or infrastructure change request
  Outputs: Docker/K8s/observability configuration files
  Success criteria: Containers build and run, health checks pass, observability stack operational
---

# Deployment Skill

## Docker

### Build & Run

```bash
docker build -f deploy/docker/Dockerfile -t dawning-agents:latest .

# Development
docker compose -f docker-compose.dev.yml up -d

# Production
docker compose -f docker-compose.prod.yml up -d
```

### Services

- **dawning-agents** — app (port 8080)
- **redis** — Redis 7.2-alpine (port 6379)
- **postgres** — PostgreSQL 16-alpine (port 5432)

## Kubernetes (`deploy/k8s/`)

```bash
cd deploy/k8s && bash deploy.sh
```

Applies: namespace → redis → configmap + secret → deployment + service → HPA → ingress.

### HPA Configuration

CPU target: 70%, Memory target: 80%, Scale: 2-10 replicas.

### Rollback

```bash
kubectl rollout undo deployment/dawning-agents -n dawning-agents
kubectl rollout undo deployment/dawning-agents -n dawning-agents --to-revision=2
```

## Observability Stack

```bash
cd deploy/observability && docker compose up -d
```

| Service | Port | Purpose |
|---------|------|---------|
| Prometheus | 9090 | Metrics |
| Grafana | 3000 | Dashboards |
| Loki | 3100 | Logs |
| Tempo | — | Traces |
| OTEL Collector | 4317/4318 | OpenTelemetry ingest |

### App Integration

```csharp
services.AddOpenTelemetryObservability(config);
services.AddAgentTracing();
services.AddAgentMetrics();
```

## Health Checks

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

