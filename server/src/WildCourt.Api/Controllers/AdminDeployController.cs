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
        RequirePermission(AdminPermissions.DeployView);
        return ApiResponse<DeployStatusResponse>.Ok(_deploy.GetStatus());
    }

    /// <summary>部署日志（最近 count 行，默认 200）。</summary>
    [HttpGet("deploy/logs")]
    public ApiResponse<List<string>> Logs([FromQuery] int count = 200)
    {
        RequirePermission(AdminPermissions.DeployView);
        return ApiResponse<List<string>>.Ok(_deploy.GetRecentLog(Math.Clamp(count, 10, 1000)));
    }

    /// <summary>上传服务端源码 zip（multipart/form-data，字段名 file），解压覆盖宿主代码目录。</summary>
    [HttpPost("deploy/upload")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ApiResponse<object>> Upload(IFormFile file)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        var count = await _deploy.UploadPackageAsync(file);
        return ApiResponse<object>.Ok(new { Files = count, Message = $"已解压 {count} 个文件，可以执行构建部署" });
    }

    /// <summary>文件管理：列出目录内容（path 为空 = 代码根目录）。</summary>
    [HttpGet("deploy/files")]
    public ApiResponse<List<FileEntryDto>> ListFiles([FromQuery] string? path)
    {
        RequirePermission(AdminPermissions.DeployView);
        return ApiResponse<List<FileEntryDto>>.Ok(_deploy.ListFiles(path ?? string.Empty));
    }

    /// <summary>文件管理：上传/覆盖单个文件到指定目录（multipart：file + path）。</summary>
    [HttpPost("deploy/file")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public ApiResponse<object> UploadFile(IFormFile file, [FromForm] string? path)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        var entry = _deploy.UploadFile(path ?? string.Empty, file);
        return ApiResponse<object>.Ok(new { entry.Path, entry.Size, Message = $"已上传 {entry.Name}" });
    }

    /// <summary>文件管理：删除文件或目录（path 相对代码根目录）。</summary>
    [HttpDelete("deploy/file")]
    public ApiResponse<object> DeleteFile([FromQuery] string path)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        _deploy.DeleteFile(path);
        return ApiResponse<object>.Ok(new { Message = "已删除" });
    }

    /// <summary>文件管理：新建目录。</summary>
    [HttpPost("deploy/mkdir")]
    public ApiResponse<object> CreateDirectory([FromBody] CreateDirectoryRequest request)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        _deploy.CreateDirectory(request.Path);
        return ApiResponse<object>.Ok(new { Message = "目录已创建" });
    }

    /// <summary>文件管理：读取文件内容（文本，2MB 以内，二进制文件拒绝）。</summary>
    [HttpGet("deploy/file/content")]
    public ApiResponse<FileContentDto> GetFileContent([FromQuery] string path)
    {
        RequirePermission(AdminPermissions.DeployView);
        return ApiResponse<FileContentDto>.Ok(_deploy.GetFileContent(path));
    }

    /// <summary>文件管理：保存文件内容（在线编辑）。</summary>
    [HttpPut("deploy/file/content")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public ApiResponse<object> SaveFileContent([FromBody] SaveFileRequest request)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        var dto = _deploy.SaveFileContent(request.Path, request.Content, request.Encoding);
        return ApiResponse<object>.Ok(new { dto.Path, dto.Size, Message = "已保存，执行构建部署后生效" });
    }

    /// <summary>触发构建部署（docker compose up -d --build）。构建中重复调用将被忽略。</summary>
    [HttpPost("deploy/build")]
    public async Task<ApiResponse<object>> Build([FromBody] DeployBuildRequest request)
    {
        RequirePermission(AdminPermissions.DeployEdit);
        await Task.Yield();
        var started = _deploy.BuildAndDeployAsync(request.NoCache, request.Service);
        if (!started)
        {
            return ApiResponse<object>.Ok(new { Started = false, Message = "已有构建任务进行中" });
        }
        return ApiResponse<object>.Ok(new { Started = true, Message = "构建已启动，请关注部署日志" });
    }
}
