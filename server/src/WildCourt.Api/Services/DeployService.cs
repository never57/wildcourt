using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 服务端部署：上传源码 zip → 解压覆盖宿主代码目录 → 按目标服务分流部署。
/// api 系列（空 / api / api-blue / api-green）走 deploy.sh 蓝绿脚本（构建 standby → 健康检查 → nginx 切换，API 无中断、失败自动回滚）；
/// 空 Service 蓝绿切换后另重建 nginx（托管前端）；其他服务走 docker compose up -d --build。
/// 生产要求：API 容器挂载宿主代码目录（/opt/wildcourt/server）到相同路径 + docker.sock + 容器内 docker CLI/compose 插件。
/// 构建在后台执行，日志保留最近 <see cref="MaxLogLines"/> 行供轮询。
/// </summary>
public class DeployService
{
    private const int MaxLogLines = 500;

    private static readonly TimeZoneInfo _cnTz = GetChinaTimeZone();

    private static TimeZoneInfo GetChinaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
    }

    /// <summary>把 UTC 时间转成中国时区（Asia/Shanghai）的格式化字符串。</summary>
    private static string ToCnTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _cnTz).ToString("yyyy-MM-dd HH:mm");

    private readonly string _hostCodeRoot;
    private readonly string _composeFile;
    private readonly string _dockerHost;
    private readonly ILogger<DeployService> _logger;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly ConcurrentQueue<string> _log = new();

    private DateTime? _lastDeployAt;
    private string _lastResult = string.Empty;
    private bool _building;

    public DeployService(IConfiguration config, ILogger<DeployService> logger)
    {
        _hostCodeRoot = config["Admin:Deploy:HostCodeRoot"] ?? "/opt/wildcourt/server";
        _composeFile = config["Admin:Deploy:ComposeFile"] ?? "docker-compose.yml";
        _dockerHost = config["Admin:DockerHost"] ?? "unix:///var/run/docker.sock";
        _logger = logger;
    }

    public DeployStatusResponse GetStatus() => new()
    {
        Enabled = !string.IsNullOrEmpty(_hostCodeRoot),
        HostCodeRoot = _hostCodeRoot,
        ComposeFile = _composeFile,
        Status = _building ? "building" : "idle",
        Building = _building,
        LastDeployAt = _lastDeployAt,
        LastDeployResult = _lastResult,
    };

    public List<string> GetRecentLog(int count = 200)
    {
        var lines = _log.ToList();
        return lines.Count > count ? lines.TakeLast(count).ToList() : lines;
    }

    /// <summary>上传 zip 包并解压覆盖宿主代码目录，返回写入文件数。</summary>
    public async Task<int> UploadPackageAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "上传文件为空");
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, "仅支持 .zip 压缩包");
        }
        if (!Directory.Exists(_hostCodeRoot))
        {
            throw new BusinessException(ApiCodes.ServerError, $"代码目录不存在：{_hostCodeRoot}（检查 Admin:Deploy:HostCodeRoot 配置）");
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var count = 0;
        var rootFull = Path.GetFullPath(_hostCodeRoot);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        // 兼容"打包带了顶层目录"（如 server/xxx）的情况：
        // 若所有条目共享唯一顶层目录，则自动剥离，使 docker-compose.yml 等落到代码根目录而非 server/server/xxx。
        var topLevel = archive.Entries
            .Select(e => e.FullName.Split('/')[0])
            .Where(n => n.Length > 0)
            .Distinct()
            .ToArray();
        var stripPrefix = topLevel.Length == 1 ? topLevel[0] + "/" : null;

        foreach (var entry in archive.Entries)
        {
            var entryName = entry.FullName;
            if (stripPrefix != null && entryName.StartsWith(stripPrefix, StringComparison.Ordinal))
            {
                entryName = entryName.Substring(stripPrefix.Length);
            }
            if (entryName.Length == 0)
            {
                continue; // 顶层目录条目本身
            }
            var fullPath = Path.GetFullPath(Path.Combine(rootFull, entryName));
            if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"压缩包含非法路径：{entry.FullName}");
            }
            if (entryName.EndsWith('/'))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var output = File.Create(fullPath);
            await using var input = entry.Open();
            await input.CopyToAsync(output);
            count++;
        }

        _logger.LogInformation("解压 {Count} 个文件到 {Root}", count, rootFull);
        return count;
    }

    // ==================== 文件管理（浏览/上传/删除/新建目录） ====================

    /// <summary>相对路径 → 绝对路径，并把结果限制在代码根目录内（防路径穿越）。</summary>
    private string ResolveSafePath(string relativePath)
    {
        var rootFull = Path.GetFullPath(_hostCodeRoot);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return rootFull;
        }
        var full = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, $"路径越界：{relativePath}");
        }
        return full;
    }

    /// <summary>列出目录内容（子目录在前，名称排序）。</summary>
    public List<FileEntryDto> ListFiles(string relativePath)
    {
        var dir = ResolveSafePath(relativePath);
        if (!Directory.Exists(dir))
        {
            throw new BusinessException(ApiCodes.BadRequest, $"目录不存在：{relativePath}");
        }

        var entries = new List<FileEntryDto>();
        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var info = new DirectoryInfo(subDir);
            entries.Add(new FileEntryDto
            {
                Name = info.Name,
                Path = Path.GetRelativePath(_hostCodeRoot, info.FullName).Replace('\\', '/'),
                Type = "dir",
                ModifiedAt = ToCnTime(info.LastWriteTimeUtc),
            });
        }
        foreach (var file in Directory.GetFiles(dir))
        {
            var info = new FileInfo(file);
            entries.Add(new FileEntryDto
            {
                Name = info.Name,
                Path = Path.GetRelativePath(_hostCodeRoot, info.FullName).Replace('\\', '/'),
                Type = "file",
                Size = info.Length,
                ModifiedAt = ToCnTime(info.LastWriteTimeUtc),
            });
        }
        return entries
            .OrderBy(e => e.Type == "dir" ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>上传/覆盖单个文件到指定目录（同名覆盖）。</summary>
    public FileEntryDto UploadFile(string relativeDir, IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "上传文件为空");
        }
        var dir = ResolveSafePath(relativeDir);
        if (!Directory.Exists(dir))
        {
            throw new BusinessException(ApiCodes.BadRequest, $"目录不存在：{relativeDir}");
        }
        var name = Path.GetFileName(file.FileName); // 剥离任何路径成分
        if (string.IsNullOrEmpty(name))
        {
            throw new BusinessException(ApiCodes.BadRequest, "文件名非法");
        }
        var full = ResolveSafePath(Path.Combine(relativeDir, name));
        if (!Directory.Exists(Path.GetDirectoryName(full)))
        {
            throw new BusinessException(ApiCodes.BadRequest, "目标目录不存在");
        }
        using (var stream = File.Create(full))
        {
            file.CopyTo(stream);
        }
        _logger.LogInformation("上传文件 {Path}（{Size} 字节）", Path.GetRelativePath(_hostCodeRoot, full), file.Length);
        return new FileEntryDto
        {
            Name = name,
            Path = Path.GetRelativePath(_hostCodeRoot, full).Replace('\\', '/'),
            Type = "file",
            Size = file.Length,
            ModifiedAt = ToCnTime(File.GetLastWriteTimeUtc(full)),
        };
    }

    /// <summary>删除文件或目录（非空目录递归删除）。根目录禁止删除。</summary>
    public void DeleteFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new BusinessException(ApiCodes.BadRequest, "路径不能为空");
        }
        var full = ResolveSafePath(relativePath);
        if (string.Equals(Path.GetFullPath(full), Path.GetFullPath(_hostCodeRoot), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能删除代码根目录");
        }
        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
        else if (File.Exists(full))
        {
            File.Delete(full);
        }
        else
        {
            throw new BusinessException(ApiCodes.BadRequest, $"路径不存在：{relativePath}");
        }
        _logger.LogWarning("删除 {Path}", relativePath);
    }

    /// <summary>新建目录（可多级）。</summary>
    public void CreateDirectory(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new BusinessException(ApiCodes.BadRequest, "目录名不能为空");
        }
        var full = ResolveSafePath(relativePath);
        Directory.CreateDirectory(full);
        _logger.LogInformation("创建目录 {Path}", relativePath);
    }

    // ==================== 文件内容（预览 / 在线编辑） ====================

    /// <summary>在线编辑支持的最大文件大小（2MB）。</summary>
    private const long MaxTextFileSize = 2 * 1024 * 1024;

    /// <summary>读取文件文本内容（2MB 以内；二进制/含 NUL 字节的文件拒绝）。</summary>
    public FileContentDto GetFileContent(string relativePath)
    {
        var full = ResolveSafePath(relativePath);
        if (!File.Exists(full))
        {
            throw new BusinessException(ApiCodes.BadRequest, $"文件不存在：{relativePath}");
        }
        var info = new FileInfo(full);
        if (info.Length > MaxTextFileSize)
        {
            throw new BusinessException(ApiCodes.BadRequest,
                $"文件过大（{info.Length} 字节），在线预览/编辑仅支持 {MaxTextFileSize / 1024 / 1024}MB 以内的文本文件");
        }

        string content;
        using (var reader = new StreamReader(full, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            content = reader.ReadToEnd();
        }
        var encoding = "utf-8";
        if (content.StartsWith("\uFEFF", StringComparison.Ordinal))
        {
            encoding = "utf-8-bom";
            content = content.Substring(1);
        }
        if (content.Contains('\0'))
        {
            throw new BusinessException(ApiCodes.BadRequest, "二进制文件（含 NUL 字节）不支持在线预览/编辑");
        }

        return new FileContentDto
        {
            Path = relativePath,
            Content = content,
            Size = info.Length,
            Encoding = encoding,
            ModifiedAt = ToCnTime(info.LastWriteTimeUtc),
        };
    }

    /// <summary>保存文件内容（在线编辑）。编码由读取接口返回后原样回传，避免破坏原文件编码。</summary>
    public FileContentDto SaveFileContent(string relativePath, string content, string encoding)
    {
        var full = ResolveSafePath(relativePath);
        if (!File.Exists(full))
        {
            throw new BusinessException(ApiCodes.BadRequest, $"文件不存在：{relativePath}");
        }
        if (content.Contains('\0'))
        {
            throw new BusinessException(ApiCodes.BadRequest, "内容包含非法字符（NUL），拒绝保存");
        }

        var enc = encoding == "utf-8-bom" ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) : new UTF8Encoding(false);
        File.WriteAllText(full, content, enc);
        _logger.LogInformation("在线编辑保存 {Path}（{Size} 字节）", relativePath, enc.GetByteCount(content));

        var info = new FileInfo(full);
        return new FileContentDto
        {
            Path = relativePath,
            Content = content,
            Size = info.Length,
            Encoding = encoding,
            ModifiedAt = ToCnTime(info.LastWriteTimeUtc),
        };
    }

    /// <summary>读取 git 仓库状态（分支 / commit / 与远端差距 / 工作区是否干净）。</summary>
    public async Task<GitStatusResponse> GetGitStatusAsync()
    {
        var resp = new GitStatusResponse();
        try
        {
            var gitRoot = FindGitRoot();
            if (gitRoot is null)
            {
                return resp; // GitRepo=false：当前代码目录不是 git 仓库
            }
            resp.GitRepo = true;

            var (code, output) = await RunGitAsync("rev-parse --abbrev-ref HEAD");
            resp.Branch = code == 0 ? output.Trim() : null;

            (code, output) = await RunGitAsync("rev-parse HEAD");
            resp.Commit = code == 0 ? output.Trim() : null;
            if (resp.Commit is { Length: >= 8 })
            {
                resp.CommitShort = resp.Commit[..8];
            }

            (code, output) = await RunGitAsync("log -1 --format=%s");
            resp.CommitMessage = code == 0 ? output.Trim() : null;

            (code, output) = await RunGitAsync("status --porcelain");
            if (code == 0)
            {
                resp.DirtyFiles = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToList();
                resp.Dirty = resp.DirtyFiles.Count > 0;
            }

            // ahead/behind（无 upstream 时命令失败，保持 0）
            (code, output) = await RunGitAsync("rev-list --left-right --count HEAD...@{upstream}");
            if (code == 0)
            {
                var parts = output.Trim().Split('\t');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out var ahead)
                    && int.TryParse(parts[1], out var behind))
                {
                    resp.Ahead = ahead;
                    resp.Behind = behind;
                }
            }
            return resp;
        }
        catch (Exception ex)
        {
            resp.Error = ex.Message;
            return resp;
        }
    }

    /// <summary>从宿主代码仓库执行 git pull（--ff-only，SSH 免密）。与构建互斥，防止拉取与部署并发改文件。</summary>
    public async Task<GitPullResponse> GitPullAsync()
    {
        await _buildLock.WaitAsync();
        try
        {
            var status = await GetGitStatusAsync();
            if (!status.GitRepo)
            {
                throw new BusinessException(ApiCodes.ServerError, "当前代码目录不是 git 仓库（未找到 .git），无法拉取");
            }
            if (string.IsNullOrEmpty(status.Branch) || status.Branch == "HEAD")
            {
                throw new BusinessException(ApiCodes.BadRequest, "当前处于 detached HEAD 状态，无法拉取，请先切回分支");
            }
            if (status.Dirty)
            {
                var files = string.Join("、", status.DirtyFiles.Take(10));
                if (status.DirtyFiles.Count > 10)
                {
                    files += $" 等 {status.DirtyFiles.Count} 个文件";
                }
                throw new BusinessException(ApiCodes.BadRequest,
                    $"工作区有未提交修改，已中止拉取（避免冲突）：{files}");
            }
            if (status.Ahead > 0)
            {
                throw new BusinessException(ApiCodes.BadRequest,
                    $"本地领先远端 {status.Ahead} 个提交且未推送，无法 fast-forward，请先在本地推送这些提交");
            }

            var gitRoot = FindGitRoot()!;
            AppendLog($"===== git pull 开始（{DateTime.Now:HH:mm:ss}） =====");
            using var process = StartGitProcess(gitRoot, "pull --ff-only");
            process.OutputDataReceived += (_, e) => AppendLog(e.Data);
            process.ErrorDataReceived += (_, e) => AppendLog(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!await WaitForExitAsync(process, TimeSpan.FromMinutes(10)))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 尽力终止
                }
                AppendLog("!! git pull 超时（10 分钟），已终止");
                throw new BusinessException(ApiCodes.ServerError, "git pull 超时（10 分钟），已终止");
            }
            AppendLog("===== git pull 结束 =====");

            if (process.ExitCode != 0)
            {
                throw new BusinessException(ApiCodes.ServerError, $"git pull 失败（exit={process.ExitCode}），详见部署日志");
            }

            var after = await GetGitStatusAsync();
            return new GitPullResponse
            {
                Success = true,
                CommitShort = after.CommitShort,
                Message = $"拉取成功：分支 {after.Branch}，当前提交 {after.CommitShort}（{after.CommitMessage}）"
                          + (after.Behind > 0 ? $"，仍落后远端 {after.Behind} 个提交" : ""),
            };
        }
        finally
        {
            _buildLock.Release();
        }
    }

    /// <summary>从 _hostCodeRoot 向上查找 git 仓库根目录（含 .git）。</summary>
    private string? FindGitRoot()
    {
        var dir = new DirectoryInfo(_hostCodeRoot);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>启动 git 进程（容器内执行：挂载的 .git 属主可能不同，需 -c safe.directory；SSH 走宿主密钥免密）。</summary>
    private Process StartGitProcess(string gitRoot, string args)
    {
        var psi = new ProcessStartInfo("git", $"-C \"{gitRoot}\" -c safe.directory=\"{gitRoot}\" {args}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["HOME"] = "/root";
        // 容器内 /root/.ssh 为宿主 SSH 密钥只读挂载（compose 已配置），ssh-rsa 算法白名单在 config 中
        psi.Environment["GIT_SSH_COMMAND"] = "ssh -o StrictHostKeyChecking=accept-new";
        return Process.Start(psi)
            ?? throw new BusinessException(ApiCodes.ServerError, "无法启动 git 进程（镜像未安装 git？）");
    }

    /// <summary>执行 git 命令并捕获输出（stderr 合并到输出末尾），超时抛业务异常。</summary>
    private async Task<(int ExitCode, string Output)> RunGitAsync(string args, int timeoutSeconds = 30)
    {
        var gitRoot = FindGitRoot()
            ?? throw new BusinessException(ApiCodes.ServerError, "未找到 git 仓库（_hostCodeRoot 向上未发现 .git 目录）");
        using var process = StartGitProcess(gitRoot, args);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(timeoutSeconds));
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 尽力终止
            }
            throw new BusinessException(ApiCodes.ServerError, $"git 命令超时（{timeoutSeconds}s）：{args}");
        }
        return (process.ExitCode, stdout + stderr);
    }

    /// <summary>触发后台构建部署（不阻塞请求）。单实例互斥，构建中重复触发会被忽略。</summary>
    public bool BuildAndDeployAsync(bool noCache, string? service)
    {
        if (_building)
        {
            return false;
        }
        _ = Task.Run(() => RunBuildAsync(noCache, service));
        return true;
    }

    private async Task RunBuildAsync(bool noCache, string? service)
    {
        if (!await _buildLock.WaitAsync(0))
        {
            return;
        }
        _building = true;
        AppendLog("===== 开始部署 =====");
        try
        {
            _lastResult = await RunDeployAsync(noCache, service);
            _lastDeployAt = DateTime.UtcNow;
            AppendLog(_lastResult);
        }
        catch (Exception ex)
        {
            _lastResult = "部署失败：" + ex.Message;
            AppendLog(_lastResult);
        }
        finally
        {
            _building = false;
            _buildLock.Release();
            AppendLog("===== 部署结束 =====");
        }
    }

    /// <summary>
    /// 按目标服务分流部署方式：
    ///  - api / api-blue / api-green / 未指定 → 蓝绿部署脚本（API 无中断切换）
    ///  - 未指定（"全部"语义）时，蓝绿切换完成后另重建 nginx（托管前端，重建窗口约几秒）
    ///  - nginx 等其他服务 → 原 docker compose up -d --build
    /// </summary>
    private async Task<string> RunDeployAsync(bool noCache, string? service)
    {
        var svc = service?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(svc) || svc is "api" or "api-blue" or "api-green")
        {
            var script = Path.Combine(_hostCodeRoot, "deploy.sh");
            if (!File.Exists(script))
            {
                throw new BusinessException(ApiCodes.ServerError, $"蓝绿部署脚本不存在：{script}");
            }

            var argLine = noCache ? "--no-cache" : "";
            await RunProcessAsync("bash", $"\"{script}\" {argLine}".TrimEnd());

            if (string.IsNullOrEmpty(svc))
            {
                // "全部发布"语义：API 蓝绿切换完成后，再重建 nginx（前端）。
                // nginx 重建窗口约几秒，管理后台短暂中断后由前端自动恢复；仅在前端有改动时需要
                await RunComposeAsync(noCache, "nginx");
            }
            return "部署成功：API 蓝绿切换完成（无中断）";
        }

        await RunComposeAsync(noCache, svc);
        return "部署成功：docker compose up -d --build 完成";
    }

    private async Task<string> RunComposeAsync(bool noCache, string service)
    {
        var composePath = Path.Combine(_hostCodeRoot, _composeFile);
        if (!File.Exists(composePath))
        {
            throw new BusinessException(ApiCodes.ServerError, $"compose 文件不存在：{composePath}");
        }

        var args = $"compose -f {composePath} up -d --build";
        if (noCache)
        {
            args += " --no-cache";
        }
        args += " " + service;

        await RunProcessAsync("docker", args);
        return "部署成功：docker compose up -d --build 完成";
    }

    /// <summary>执行部署子进程，实时转发日志到部署日志队列，超时/失败抛业务异常。</summary>
    private async Task RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["DOCKER_HOST"] = _dockerHost;

        using var process = Process.Start(psi)
            ?? throw new BusinessException(ApiCodes.ServerError, $"无法启动进程：{fileName}");
        process.OutputDataReceived += (_, e) => AppendLog(e.Data);
        process.ErrorDataReceived += (_, e) => AppendLog(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!await WaitForExitAsync(process, TimeSpan.FromMinutes(30)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 尽力终止
            }
            throw new BusinessException(ApiCodes.ServerError, "部署超时（30 分钟），已终止构建");
        }
        if (process.ExitCode != 0)
        {
            throw new BusinessException(ApiCodes.ServerError, $"部署失败（exit={process.ExitCode}），详见部署日志");
        }
    }

    private void AppendLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }
        _log.Enqueue($"[{DateTime.Now:HH:mm:ss}] {line}");
        while (_log.Count > MaxLogLines)
        {
            _log.TryDequeue(out _);
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
