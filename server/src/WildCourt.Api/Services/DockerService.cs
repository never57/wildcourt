using System.Diagnostics;
using System.Text.Json;
using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 服务器监控：通过 docker CLI 获取容器/镜像信息，通过挂载的宿主 /proc 与根目录读取资源。
/// 生产部署时 API 容器需挂载 /var/run/docker.sock 与宿主根目录到 /host、宿主 /proc 到 /host/proc。
/// 本地无 docker 环境时优雅降级（返回空数据，不抛异常）。
/// </summary>
public class DockerService
{
    private readonly ILogger<DockerService> _logger;
    private readonly string _dockerHost;

    public DockerService(ILogger<DockerService> logger, IConfiguration config)
    {
        _logger = logger;
        // 容器内默认走本机 docker.sock；也可用 tcp:// 指向远程（不推荐）
        _dockerHost = config["Admin:DockerHost"] ?? "unix:///var/run/docker.sock";
    }

    /// <summary>Docker 是否可用（本机无 docker 时返回 false，前端隐藏监控模块入口）。</summary>
    public async Task<bool> IsDockerReadyAsync()
    {
        try
        {
            var (code, _, _) = await RunDockerAsync("info --format \"{{.ServerVersion}}\"", 10, false);
            return code == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 执行 docker 命令。DOCKER_HOST 由构造函数配置；throwOnError=false 时失败返回 (-1, stdout, 错误消息)。
    /// </summary>
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunDockerAsync(string args, int timeoutSec = 30, bool throwOnError = true)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.Environment["DOCKER_HOST"] = _dockerHost;

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 docker 进程");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (throwOnError && process.ExitCode != 0)
            {
                throw new BusinessException(ApiCodes.ServerError, $"docker {args} 失败：{stderr.Trim()}");
            }
            return (process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            throw new BusinessException(ApiCodes.ServerError, $"docker {args} 执行超时（{timeoutSec}s）");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            if (throwOnError)
            {
                throw new BusinessException(ApiCodes.ServerError, $"docker 不可用：{ex.Message}");
            }
            return (-1, string.Empty, ex.Message);
        }
    }

    /// <summary>容器列表（docker ps -a 的 json 输出，含已停止容器）。</summary>
    public async Task<List<ContainerInfoDto>> ListContainersAsync()
    {
        var (exit, stdout, _) = await RunDockerAsync("ps -a --format json", 30, false);
        if (exit != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            return new List<ContainerInfoDto>();
        }

        var containers = new List<ContainerInfoDto>();
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                containers.Add(new ContainerInfoDto
                {
                    ContainerId = (r.TryGetProperty("ID", out var id) ? id.GetString() : string.Empty) ?? string.Empty,
                    Name = (r.TryGetProperty("Names", out var n) ? n.GetString() : string.Empty) ?? string.Empty,
                    Image = (r.TryGetProperty("Image", out var img) ? img.GetString() : string.Empty) ?? string.Empty,
                    State = (r.TryGetProperty("State", out var st) ? st.GetString() : string.Empty) ?? string.Empty,
                    Status = (r.TryGetProperty("Status", out var status) ? status.GetString() : string.Empty) ?? string.Empty,
                    Ports = (r.TryGetProperty("Ports", out var p) ? p.GetString() : string.Empty) ?? string.Empty,
                    RunningFor = (r.TryGetProperty("RunningFor", out var rf) ? rf.GetString() : string.Empty) ?? string.Empty,
                });
            }
            catch (JsonException)
            {
                // 跳过无法解析的行
            }
        }
        return containers;
    }

    /// <summary>容器实时资源（docker stats --no-stream）。</summary>
    public async Task<Dictionary<string, ContainerInfoDto>> GetStatsAsync()
    {
        var (exit, stdout, _) = await RunDockerAsync("stats --no-stream --format json", 30, false);
        var result = new Dictionary<string, ContainerInfoDto>();
        if (exit != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            return result;
        }

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                var cid = (r.TryGetProperty("ID", out var id) ? id.GetString() : string.Empty) ?? string.Empty;
                var name = (r.TryGetProperty("Name", out var n) ? n.GetString() : string.Empty) ?? string.Empty;
                var memUsage = (r.TryGetProperty("MemUsage", out var mu) ? mu.GetString() : string.Empty) ?? string.Empty;
                var cpuPerc = (r.TryGetProperty("CPUPerc", out var cpu) ? cpu.GetString() : string.Empty) ?? string.Empty;
                var memLimit = (r.TryGetProperty("MemLimit", out var ml) ? ml.GetString() : string.Empty) ?? string.Empty;

                var (used, limit) = ParseMemUsage(memUsage);
                result[StringId(cid, name)] = new ContainerInfoDto
                {
                    ContainerId = cid,
                    Name = name,
                    CpuPercent = ParsePercent(cpuPerc),
                    MemUsage = used,
                    MemLimit = limit > 0 ? limit : ParseBytes(memLimit),
                };
            }
            catch (JsonException)
            {
                // 跳过无法解析的行
            }
        }
        return result;
    }

    /// <summary>容器日志（tail 最近 N 行）。</summary>
    public async Task<ContainerLogsResponse> GetLogsAsync(string nameOrId, int tail = 300)
    {
        var (exit, stdout, stderr) = await RunDockerAsync($"logs --tail {tail} {nameOrId} 2>&1", 20, false);
        return new ContainerLogsResponse
        {
            ContainerName = nameOrId,
            Log = exit == 0 ? stdout : stderr.Trim(),
        };
    }

    /// <summary>重启容器。</summary>
    public async Task RestartContainerAsync(string nameOrId)
    {
        await RunDockerAsync($"restart {nameOrId}", 60);
    }

    /// <summary>宿主机概览：内存/负载/磁盘 + Docker 版本与容器资源。数据来自挂载的 /host 与 /host/proc。</summary>
    public async Task<ServerOverviewResponse> GetHostOverviewAsync()
    {
        var overview = new ServerOverviewResponse();

        // 宿主内存
        var meminfo = await ReadTextAsync("/host/proc/meminfo");
        var memTotalKb = ParseKbValue(meminfo, "MemTotal");
        var memAvailKb = ParseKbValue(meminfo, "MemAvailable");
        overview.MemTotal = memTotalKb * 1024;
        overview.MemUsed = Math.Max(0, (memTotalKb - memAvailKb)) * 1024;
        overview.MemUsagePercent = memTotalKb > 0 ? (double)(memTotalKb - memAvailKb) / memTotalKb : 0;

        // 宿主负载
        var loadavg = await ReadTextAsync("/host/proc/loadavg");
        var loadParts = loadavg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (loadParts.Length >= 3)
        {
            double.TryParse(loadParts[0], out var l1);
            double.TryParse(loadParts[1], out var l5);
            double.TryParse(loadParts[2], out var l15);
            overview.LoadAvg1 = l1;
            overview.LoadAvg5 = l5;
            overview.LoadAvg15 = l15;
        }

        // 宿主机名与 Docker 版本
        var (dfCode, dfOut, _) = await RunDockerAsync("info --format \"{{.Name}}\"", 10, false);
        overview.HostName = dfCode == 0 ? dfOut.Trim() : Environment.MachineName;
        var (vCode, vOut, _) = await RunDockerAsync("info --format \"{{.ServerVersion}}\"", 10, false);
        overview.DockerVersion = vCode == 0 ? vOut.Trim() : string.Empty;

        // 宿主磁盘：容器已挂载宿主根到 /host（df 基于 statfs，报告宿主根分区真实统计）；
        // 未挂载时尝试用一次性 alpine 容器兜底，再失败则置空。
        var diskOut = await RunHostDfAsync();
        ParseDfOutput(diskOut, overview);

        // Docker 磁盘占用摘要
        var (dfDockerExit, dfDockerOut, _) = await RunDockerAsync("system df", 20, false);
        overview.DockerDiskSummary = dfDockerExit == 0 ? dfDockerOut.Trim() : string.Empty;

        // 容器列表 + 实时资源
        var containers = await ListContainersAsync();
        var stats = await GetStatsAsync();
        foreach (var c in containers)
        {
            if (stats.TryGetValue(StringId(c.ContainerId, c.Name), out var s))
            {
                c.CpuPercent = s.CpuPercent;
                c.MemUsage = s.MemUsage;
                c.MemLimit = s.MemLimit;
            }
        }
        overview.Containers = containers;

        return overview;
    }

    // ============ 解析工具 ============

    /// <summary>执行宿主根分区 df。优先容器内直接执行（/host 挂载），失败回退一次性 alpine 容器。</summary>
    private async Task<string> RunHostDfAsync()
    {
        var psi = new ProcessStartInfo("df", "-k /host")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var process = Process.Start(psi);
            if (process is not null)
            {
                var stdout = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0 && stdout.Contains("/host"))
                {
                    return stdout;
                }
            }
        }
        catch
        {
            // 容器内无 df，走 docker run 兜底
        }

        var (exit, out2, _) = await RunDockerAsync("run --rm -v /host:/host:ro alpine df -k /host", 30, false);
        return exit == 0 ? out2 : string.Empty;
    }

    private static async Task<string> ReadTextAsync(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long ParseKbValue(string content, string key)
    {
        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', 2);
                var token = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0";
                return long.TryParse(token, out var v) ? v : 0;
            }
        }
        return 0;
    }

    private static void ParseDfOutput(string output, ServerOverviewResponse overview)
    {
        foreach (var line in output.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Filesystem 1K-blocks Used Available Use% Mounted on（Filesystem 可能含空格，从右解析）
            if (parts.Length < 6)
            {
                continue;
            }
            var blocks = long.Parse(parts[parts.Length - 5]);
            var used = long.Parse(parts[parts.Length - 4]);
            overview.DiskTotal = blocks * 1024;
            overview.DiskUsed = used * 1024;
            overview.DiskUsagePercent = blocks > 0 ? (double)used / blocks : 0;
            overview.DiskMountPoint = "/";
            break;
        }
    }

    /// <summary>解析 docker stats MemUsage "7.5MiB / 128MiB"。</summary>
    private static (long used, long limit) ParseMemUsage(string memUsage)
    {
        if (string.IsNullOrWhiteSpace(memUsage))
        {
            return (0, 0);
        }
        var parts = memUsage.Split('/');
        var used = ParseBytes(parts[0].Trim());
        var limit = parts.Length > 1 ? ParseBytes(parts[1].Trim()) : 0;
        return (used, limit);
    }

    private static long ParseBytes(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }
        input = input.Trim();
        var unitIndex = input.Length - 1;
        char unit = input[^1];
        if (!char.IsDigit(unit))
        {
            var num = input[..unitIndex].Trim();
            if (double.TryParse(num, out var value))
            {
                return unit switch
                {
                    'K' => (long)(value * 1024),
                    'M' => (long)(value * 1024 * 1024),
                    'G' => (long)(value * 1024 * 1024 * 1024),
                    'T' => (long)(value * 1024L * 1024 * 1024 * 1024),
                    'B' => (long)value,
                    _ => (long)value,
                };
            }
            return 0;
        }
        return long.TryParse(input, out var v) ? v : 0;
    }

    private static double? ParsePercent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        var cleaned = input.Trim().TrimEnd('%').Trim();
        return double.TryParse(cleaned, out var v) ? v : null;
    }

    private static string StringId(string containerId, string name) =>
        string.IsNullOrEmpty(containerId) ? name : containerId;
}
