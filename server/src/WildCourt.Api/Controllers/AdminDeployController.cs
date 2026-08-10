using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>服务端部署：上传源码 zip → 构建并重启容器。构建在后台执行。</summary>
public class AdminDeployController : AdminControllerBase
{
    private readonly DeployService _deploy;

    public AdminDeployController(DeployService deploy)
    {
        _deploy = deploy;
    }

    /// <summary>部署状态（目录/构建中/上次结果）。</summary>
    [HttpGet("deploy/status")]
    public ApiResponse<DeployStatusResponse> Status()
    {
        return ApiResponse<DeployStatusResponse>.Ok(_deploy.GetStatus());
    }

    /// <summary>部署日志（最近 count 行，默认 200）。</summary>
    [HttpGet("deploy/logs")]
    public ApiResponse<List<string>> Logs([FromQuery] int count = 200)
    {
        return ApiResponse<List<string>>.Ok(_deploy.GetRecentLog(Math.Clamp(count, 10, 1000)));
    }

    /// <summary>上传服务端源码 zip（multipart/form-data，字段名 file），解压覆盖宿主代码目录。</summary>
    [HttpPost("deploy/upload")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ApiResponse<object>> Upload(IFormFile file)
    {
        var count = await _deploy.UploadPackageAsync(file);
        return ApiResponse<object>.Ok(new { Files = count, Message = $"已解压 {count} 个文件，可以执行构建部署" });
    }

    /// <summary>触发构建部署（docker compose up -d --build）。构建中重复调用将被忽略。</summary>
    [HttpPost("deploy/build")]
    public async Task<ApiResponse<object>> Build([FromBody] DeployBuildRequest request)
    {
        await Task.Yield();
        var started = _deploy.BuildAndDeployAsync(request.NoCache, request.Service);
        if (!started)
        {
            return ApiResponse<object>.Ok(new { Started = false, Message = "已有构建任务进行中" });
        }
        return ApiResponse<object>.Ok(new { Started = true, Message = "构建已启动，请关注部署日志" });
    }
}
