#!/bin/bash
set -e

# /root/.ssh 是宿主 SSH 密钥的只读挂载（属主为宿主用户，如 ubuntu/UID 1000）。
# OpenSSH 严格校验 config 文件属主必须是 root 或当前用户，否则报
# "Bad owner or permissions on /root/.ssh/config"，导致 git pull SSH 免密失败。
# 这里在容器启动时把 .ssh 复制到可写目录并修复属主/权限，git pull 走修复后的 config。
rm -rf /root/.ssh-runtime
mkdir -p /root/.ssh-runtime
if [ -d /root/.ssh ]; then
  cp -a /root/.ssh/. /root/.ssh-runtime/
fi
chown -R root:root /root/.ssh-runtime
chmod 700 /root/.ssh-runtime
# 私钥/config 收紧为 600（known_hosts 等保持默认即可）
chmod 600 /root/.ssh-runtime/config /root/.ssh-runtime/id_* 2>/dev/null || true
# config 中 IdentityFile 形如 ~/.ssh/xxx，重写到修复后的目录，避免仍访问只读挂载点
sed -i 's|~/.ssh/|~/.ssh-runtime/|g' /root/.ssh-runtime/config 2>/dev/null || true
touch /root/.ssh-runtime/config

exec dotnet WildCourt.Api.dll
