# ----------- Build Stage -----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制 csproj 并还原依赖
COPY src/Dawning.Agents.Abstractions/*.csproj src/Dawning.Agents.Abstractions/
COPY src/Dawning.Agents.Core/*.csproj src/Dawning.Agents.Core/
COPY src/Dawning.Agents.Redis/*.csproj src/Dawning.Agents.Redis/
RUN dotnet restore src/Dawning.Agents.Core/Dawning.Agents.Core.csproj

# 复制所有源代码并构建
COPY . .
RUN dotnet publish src/Dawning.Agents.Core/Dawning.Agents.Core.csproj -c Release -o /app/publish --no-restore

# ----------- Runtime Stage -----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# 复制发布输出
COPY --from=build /app/publish .

# 健康检查（可选，需在 docker-compose 中启用）
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Dawning.Agents.Core.dll"]
