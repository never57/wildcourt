# WildCourt 服务端 Docker 镜像（.NET 10 Web API）
# 构建：在 server/ 目录下 docker build -t wildcourt-api .
# 运行建议用 docker-compose.yml（含 MySQL/Redis 依赖）

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先拷贝 csproj 做 restore 缓存
COPY WildCourt.slnx ./
COPY src/WildCourt.Api/WildCourt.Api.csproj src/WildCourt.Api/
COPY src/WildCourt.Data/WildCourt.Data.csproj src/WildCourt.Data/
COPY src/WildCourt.Tests/WildCourt.Tests.csproj src/WildCourt.Tests/
RUN dotnet restore src/WildCourt.Api/WildCourt.Api.csproj

# 拷贝源码并发布
COPY src/ src/
RUN dotnet publish src/WildCourt.Api -c Release -o /app/publish

# 运行镜像
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "WildCourt.Api.dll"]
