using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace WildCourt.Mcp.Tools;

/// <summary>服务端部署工具：状态 / 日志 / 构建 / 源码文件在线编辑。</summary>
[McpServerToolType]
public class DeployTools
{
    private static AdminApiClient _api = null!;

    public static Task InitializeAsync(AdminApiClient api)
    {
        _api = api;
        return Task.CompletedTask;
    }

    private static string Json(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>查询服务端部署状态（当前是否在构建、上次部署时间与结果、代码目录）。</summary>
    [McpServerTool(Name = "deploy_status"), Description("查询服务端部署状态：是否在构建中、上次部署时间与结果、宿主代码目录。")]
    public async Task<string> Status()
    {
        return Json(JsonDocument.Parse(await _api.GetAsync("deploy/status")).RootElement);
    }

    /// <summary>查询最近的部署日志（构建输出）。</summary>
    [McpServerTool(Name = "deploy_logs"), Description("查询最近的部署日志（构建输出），count 为行数，范围 10~1000，默认 200。")]
    public async Task<string> Logs(
        [Description("日志行数，10~1000，默认 200")] int? count = null)
    {
        var query = new Dictionary<string, string?>();
        if (count is not null)
        {
            query["count"] = count.Value.ToString();
        }
        var raw = await _api.GetAsync("deploy/logs", query);
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>
    /// 触发服务端部署（后台执行），构建中重复调用会被忽略。
    /// Service 为空或 api / api-blue / api-green 时走蓝绿部署脚本（构建 standby → 健康检查 → nginx 切换，API 无中断、失败自动回滚）；
    /// Service 为空时蓝绿切换完成后还会重建 nginx（托管前端，重建窗口约几秒）。
    /// 其他服务（如 nginx / redis / mysql）走 docker compose up -d --build。
    /// </summary>
    [McpServerTool(Name = "deploy_build"), Description("触发服务端部署（后台执行）。Service 为空或 api/api-blue/api-green 时走蓝绿部署脚本（API 无中断、失败自动回滚）；Service 为空时蓝绿切换后还会重建 nginx（前端）；其他服务走 docker compose up -d --build。NoCache=true 强制无缓存重建；构建中重复调用会被忽略。")]
    public async Task<string> Build(
        [Description("是否无缓存重建镜像，默认 false")] bool? noCache = null,
        [Description("可选：compose 服务名。空=全部（API 蓝绿切换 + 重建 nginx）；api/api-blue/api-green=仅 API 蓝绿切换；nginx 等其他服务名=直接重建该服务")] string? service = null)
    {
        var body = new { NoCache = noCache ?? false, Service = service };
        var raw = await _api.PostAsync("deploy/build", body);
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>列出服务端源码目录内容（相对代码根目录）。</summary>
    [McpServerTool(Name = "deploy_list_files"), Description("列出服务端源码目录内容，path 为相对代码根目录的子目录（空=根目录）。返回文件名/类型/大小/修改时间。")]
    public async Task<string> ListFiles(
        [Description("相对代码根目录的路径，空=根目录")] string? path = null)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrEmpty(path))
        {
            query["path"] = path;
        }
        var raw = await _api.GetAsync("deploy/files", query);
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>读取服务端源码文件内容（文本，2MB 以内；二进制文件会拒绝）。</summary>
    [McpServerTool(Name = "deploy_read_file"), Description("读取服务端源码文件内容（文本，2MB 以内）。返回内容、大小、编码。二进制文件会被拒绝。")]
    public async Task<string> ReadFile(
        [Description("相对代码根目录的文件路径，如 src/WildCourt.Api/Program.cs")] string path)
    {
        var raw = await _api.GetAsync("deploy/file/content", new Dictionary<string, string?> { ["path"] = path });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>保存/覆盖服务端源码文件内容（在线编辑），保存后需执行构建才生效。</summary>
    [McpServerTool(Name = "deploy_save_file"), Description("保存/覆盖服务端源码文件内容（在线编辑）。Encoding 由 deploy_read_file 返回后原样回传（utf-8 / utf-8-bom）。保存后需执行 deploy_build 才生效。")]
    public async Task<string> SaveFile(
        [Description("相对代码根目录的文件路径")] string path,
        [Description("新的文件内容")] string content,
        [Description("文件编码：utf-8 / utf-8-bom，默认 utf-8")] string? encoding = "utf-8")
    {
        var raw = await _api.PutAsync("deploy/file/content", new { Path = path, Content = content, Encoding = encoding ?? "utf-8" });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>在服务端源码目录新建子目录。</summary>
    [McpServerTool(Name = "deploy_mkdir"), Description("在服务端源码目录新建子目录，如 src/WildCourt.Api/Services。")]
    public async Task<string> Mkdir(
        [Description("相对代码根目录的新目录路径")] string path)
    {
        var raw = await _api.PostAsync("deploy/mkdir", new { Path = path });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>删除服务端源码中的文件或目录（不可恢复，请谨慎）。</summary>
    [McpServerTool(Name = "deploy_delete_file"), Description("删除服务端源码中的文件或目录（相对代码根目录）。不可恢复，操作前请向用户确认。")]
    public async Task<string> DeleteFile(
        [Description("相对代码根目录的文件/目录路径")] string path)
    {
        var raw = await _api.DeleteAsync("deploy/file", new Dictionary<string, string?> { ["path"] = path });
        return Json(JsonDocument.Parse(raw).RootElement);
    }
}
