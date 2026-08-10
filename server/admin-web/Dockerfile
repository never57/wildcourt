# WildCourt 管理后台前端（多阶段构建：node 构建 → nginx 托管）
# 构建上下文：server/admin-web/
# 用法：docker compose 内通过 nginx 服务构建；或单独构建
#   docker build -t wildcourt-admin -f admin-web/Dockerfile admin-web
# 国内服务器构建慢时可指定镜像源：docker compose build --build-arg NPM_REGISTRY=https://registry.npmmirror.com nginx

# ---- Stage 1: 构建静态资源 ----
FROM node:20-alpine AS build
WORKDIR /app

# 依赖层缓存：仅当 package.json / package-lock.json 变化时才重新 npm ci
COPY package.json package-lock.json ./
ARG NPM_REGISTRY=https://registry.npmjs.org
RUN npm ci --registry=${NPM_REGISTRY}

COPY . .
RUN npm run build

# ---- Stage 2: Nginx 托管 ----
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
