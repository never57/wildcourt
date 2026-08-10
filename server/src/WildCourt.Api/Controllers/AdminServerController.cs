using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>服务器监控：宿主机资源、Docker 容器状态/日志/重启。</summary>
public class AdminServerController : AdminControllerBase
{
    private readonly DockerService _docker;

    public AdminServerController(DockerService docker)
    {
        _docker = docker;
    }

    /// <summary>宿主机概览 + 容器列表与实时资源。</summary>
    [HttpGet("server/overview")]
    public async Task<ApiResponse<ServerOverviewResponse>> Overview()
    {
        return ApiResponse<ServerOverviewResponse>.Ok(await _docker.GetHostOverviewAsync());
    }

    /// <summary>Docker 容器列表。</summary>
    [HttpGet("server/containers")]
    public async Task<ApiResponse<List<ContainerInfoDto>>> Containers()
    {
        return ApiResponse<List<ContainerInfoDto>>.Ok(await _docker.ListContainersAsync());
    }

    /// <summary>容器日志（tail N 行）。</summary>
    [HttpGet("server/containers/{nameOrId}/logs")]
    public async Task<ApiResponse<ContainerLogsResponse>> Logs(string nameOrId, [FromQuery] int tail = 300)
    {
        return ApiResponse<ContainerLogsResponse>.Ok(await _docker.GetLogsAsync(nameOrId, Math.Clamp(tail, 10, 2000)));
    }

    /// <summary>重启容器。</summary>
    [HttpPost("server/containers/{nameOrId}/restart")]
    public async Task<ApiResponse<object>> Restart(string nameOrId)
    {
        await _docker.RestartContainerAsync(nameOrId);
        return ApiResponse<object>.Ok(new object());
    }
}
