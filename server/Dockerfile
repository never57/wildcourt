# WildCourt 服务端 Docker 镜像（.NET 10 Web API）
# 构建：在 server/ 目录下 docker build -t wlidcourt-api .
# 运行建议用 docker-compose.yml（含 MySQL/Redis 依赖）

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先拷贝 csproj 做 restore 缓存
COPY WlidCourt.sln ./
COPY src/WlidCourt.Api/WlidCourt.Api.csproj src/WlidCourt.Api/
COPY src/WlidCourt.Data/WlidCourt.Data.csproj src/WlidCourt.Data/
COPY src/WlidCourt.Tests/WlidCourt.Tests.csproj src/WlidCourt.Tests/
RUN dotnet restore src/WlidCourt.Api/WlidCourt.Api.csproj

# 拷贝源码并发布
COPY src/ src/
RUN dotnet publish src/WlidCourt.Api -c Release -o /app/publish

# 运行镜像
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "WlidCourt.Api.dll"]
