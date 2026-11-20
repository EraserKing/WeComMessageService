namespace Synology.Exceptions
{
    public class SynologyApiException : Exception
    {
        public string ApiName { get; }
        public string Method { get; }
        public int ErrorCode { get; }

        public SynologyApiException(string apiName, string method, int errorCode)
            : base(Describe(apiName, method, errorCode))
        {
            ApiName = apiName;
            Method = method;
            ErrorCode = errorCode;
        }

        private static string Describe(string apiName, string method, int errorCode)
        {
            string description = errorCode switch
            {
                400 => "用户名或密码错误",
                401 => "账号已被停用",
                402 => "没有权限",
                403 => "需要 OTP 验证码",
                404 => "OTP 验证码错误",
                405 => "App Portal 设置不正确",
                406 => "需要 OTP 验证码",
                407 => "登录尝试次数过多，账号可能已被暂时锁定",
                408 => "密码已过期",
                409 => "密码已过期",
                410 => "需要先修改密码",
                411 => "账号已被锁定",
                100 => "未知错误",
                101 => "缺少必要参数",
                102 => "不支持的 API",
                103 => "不支持的 API 方法",
                104 => "不支持的 API 版本",
                105 => "权限不足",
                106 => "登录会话已超时",
                107 => "登录会话被其他登录中断",
                114 => "缺少必要参数",
                117 => "DSM 内部错误",
                120 => "参数无效",
                160 => "应用权限不足",
                _ => $"DSM 返回错误码 {errorCode}"
            };

            return $"{description} ({apiName}.{method})";
        }
    }
}
