using System;

namespace WildCourt.Net.Protocol
{
    /// <summary>
    /// 公告 DTO（服务端 AnnouncementDto，camelCase 输出）。
    /// 经 GET /api/v1/announcements 公开接口获取，免登录。
    /// </summary>
    [Serializable]
    public class AnnouncementDto
    {
        public long announcementId;
        public string title;
        public string content;
        public string status;
        public string creatorName;

        /// <summary>ISO 时间字符串（服务端 DateTime 序列化）。</summary>
        public string createdAt;

        /// <summary>发布时间，可为空。</summary>
        public string publishedAt;
    }
}
