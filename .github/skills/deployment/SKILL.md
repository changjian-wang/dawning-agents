---
description: "Deployment infrastructure for Dawning.Agents: Docker, K8s, observability, rollback. Trigger: 部署, deploy, docker, k8s, kubernetes, 容器, container, helm, rollback, 回滚, observability, 可观测性, grafana, prometheus"
---

# Deployment Skill

## 目标

提供 Docker、Kubernetes 部署和可观测性基础设施的操作指南。

## 触发条件

- **关键词**：部署, deploy, docker, k8s, kubernetes, 容器, container, helm, rollback, 回滚, grafana, prometheus
- **文件模式**：`Dockerfile`, `docker-compose*.yml`, `deploy/**`, `*.yaml`
- **用户意图**：部署应用、配置 K8s、设置可观测性、回滚版本

## 编排

- **前置**：`nuget-release`（发布后部署）
- **后续**：`troubleshooting`（部署出问题时）

---

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

## 验收场景

- **输入**："帮我部署到 K8s"
- **预期**：agent 指导执行 `deploy.sh`，验证 Pod 状态和健康检查
- **上次验证**：2026-02-27
