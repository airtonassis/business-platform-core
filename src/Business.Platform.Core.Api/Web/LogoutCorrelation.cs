using System.Security.Cryptography;
using System.Text.Json;

namespace Business.Platform.Core.Api.Web;

public static class LogoutCorrelation
{
    public static string Hash(params string[] values)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
}
