using System;

namespace WildCourt.Net.Protocol
{
    /// <summary>
    /// 服务端统一响应信封（文档 02 §2.2）：<c>{ code, data, message }</c>。
    /// <c>code == 0</c> 成功；非 0 为业务/系统错误，见 <see cref="ApiCodes"/>。
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public int code;
        public T data;
        public string message;

        public bool IsSuccess => code == ApiCodes.Success;
    }

    /// <summary>仅解析信封头（code/message），用于无法确定 data 类型时的错误判断。</summary>
    [Serializable]
    public class ApiEnvelope
    {
        public int code;
        public string message;

        public bool IsSuccess => code == ApiCodes.Success;
    }
}
