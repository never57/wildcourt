# WildCourt 服务端 Docker 镜像（.NET 10 Web API）
# 构建：在 server/ 目录下 docker build -t wildcourt-api .
# 运行建议用 docker-compose.yml（含 MySQL/Redis 依赖）
#
# 管理后台依赖：runtime 镜像内置 docker CLI + compose 插件，
# 配合挂载宿主 docker.sock 后可执行容器监控/日志/重启与 compose 部署。
# 安全提醒：docker.sock 等于宿主机 root 权限，管理后台务必绑定内网/Nginx+鉴权使用。

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

# 国内服务器网络说明：
#   apt、docker CLI、compose 插件全部走阿里云镜像，不访问 GitHub：
#   docker CLI + compose 插件来自阿里云 docker-ce apt 仓库（docker-ce-cli / docker-compose-plugin）。
#   .NET 10 官方默认镜像基于 Ubuntu 24.04 (noble)，若未来基础镜像变化，
#   可用 --build-arg 覆盖镜像源或代号：
#     docker compose build --build-arg APT_MIRROR=... --build-arg UBUNTU_CODENAME=... api
ARG APT_MIRROR=https://mirrors.aliyun.com
ARG UBUNTU_CODENAME=noble

# 安装 curl + git（apt 源切换为阿里云镜像加速；git 供管理后台"拉取代码"执行 git pull 用）
RUN sed -i "s|http://archive.ubuntu.com/ubuntu|${APT_MIRROR}/ubuntu|g; s|http://security.ubuntu.com/ubuntu|${APT_MIRROR}/ubuntu|g" \
        /etc/apt/sources.list.d/ubuntu.sources /etc/apt/sources.list 2>/dev/null; \
    apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates git \
    && rm -rf /var/lib/apt/lists/*

# 通过阿里云 docker-ce 仓库安装 docker CLI + compose 插件（管理后台监控/部署使用，无需访问 GitHub）
RUN install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL ${APT_MIRROR}/docker-ce/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc \
    && chmod a+r /etc/apt/keyrings/docker.asc \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] ${APT_MIRROR}/docker-ce/linux/ubuntu ${UBUNTU_CODENAME} stable" > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli docker-compose-plugin \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 5000
ENTRYPOINT ["dotnet", "WildCourt.Api.dll"]
