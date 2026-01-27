#!/bin/bash
# Kubernetes 部署脚本

set -e

NAMESPACE="dawning-agents"

echo "=== 部署 Dawning Agents 到 Kubernetes ==="

# 创建命名空间
echo "1. 创建命名空间..."
kubectl apply -f namespace.yaml

# 部署 Redis
echo "2. 部署 Redis..."
kubectl apply -f redis.yaml

# 创建配置和密钥
echo "3. 创建 ConfigMap 和 Secret..."
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml

# 部署应用
echo "4. 部署应用..."
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml

# 配置 HPA
echo "5. 配置 HPA..."
kubectl apply -f hpa.yaml

# 配置 Ingress
echo "6. 配置 Ingress..."
kubectl apply -f ingress.yaml

echo "=== 部署完成 ==="
echo ""
echo "查看 Pod 状态:"
echo "  kubectl get pods -n $NAMESPACE"
echo ""
echo "查看服务:"
echo "  kubectl get svc -n $NAMESPACE"
echo ""
echo "查看 HPA:"
echo "  kubectl get hpa -n $NAMESPACE"
