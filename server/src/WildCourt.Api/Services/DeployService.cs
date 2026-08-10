using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 服务端部署：上传源码 zip → 解压覆盖宿主代码目录 → docker compose up -d --build。
/// 生产要求：API 容器挂载宿主代码目录（/opt/wildcourt/server）到相同路径 + docker.sock + 容器内 docker CLI/compose 插件。
/// 构建在后台执行，日志保留最近 <see cref="MaxLogLines"/> 行供轮询。
/// </summary>
public class DeployService
{
    private const int MaxLogLines = 500;

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
        foreach (var entry in archive.Entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(rootFull, entry.FullName));
            if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"压缩包含非法路径：{entry.FullName}");
            }
            if (entry.FullName.EndsWith('/'))
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
            _lastResult = await RunComposeAsync(noCache, service);
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

    private async Task<string> RunComposeAsync(bool noCache, string? service)
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
        if (!string.IsNullOrWhiteSpace(service))
        {
            args += " " + service.Trim();
        }

        var psi = new ProcessStartInfo("docker", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["DOCKER_HOST"] = _dockerHost;

        using var process = Process.Start(psi)
            ?? throw new BusinessException(ApiCodes.ServerError, "无法启动 docker 进程");
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
        return "部署成功：docker compose up -d --build 完成";
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
