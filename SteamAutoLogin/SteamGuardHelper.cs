using System;
using System.IO;
using Newtonsoft.Json;

public static class SteamGuardHelper
{
    /// <summary>
    /// 从 maFiles 目录中匹配账号，返回对应的 .maFile 路径
    /// </summary>
    public static string FindMaFileForAccount(string maFilesDir, string username)
    {
        if (!Directory.Exists(maFilesDir))
            throw new DirectoryNotFoundException("maFiles 目录不存在: " + maFilesDir);

        foreach (var file in Directory.GetFiles(maFilesDir, "*.maFile"))
        {
            var json = File.ReadAllText(file);
            dynamic maFile = JsonConvert.DeserializeObject(json);
            if ((string)maFile.account_name == username)
                return file;
        }
        return null;
    }

    /// <summary>
    /// 生成 Steam 手机令牌验证码（含大写字母，官方算法）
    /// </summary>
    public static string GetSteamGuardCode(string maFilePath)
    {
        var json = File.ReadAllText(maFilePath);
        dynamic maFile = JsonConvert.DeserializeObject(json);
        string shared_secret = maFile.shared_secret;
        return GenerateSteamGuardCode(shared_secret);
    }

    /// <summary>
    /// 核心算法：Steam 令牌官方算法
    /// </summary>
    public static string GenerateSteamGuardCode(string sharedSecret)
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var time = BitConverter.GetBytes((ulong)(unixTime / 30));
        if (BitConverter.IsLittleEndian)
            Array.Reverse(time);

        var sharedSecretBytes = Convert.FromBase64String(sharedSecret);

        using (var hmac = new System.Security.Cryptography.HMACSHA1(sharedSecretBytes))
        {
            var hash = hmac.ComputeHash(time);

            int offset = hash[hash.Length - 1] & 0x0F;
            int codeInt = ((hash[offset] & 0x7f) << 24)
                        | ((hash[offset + 1] & 0xff) << 16)
                        | ((hash[offset + 2] & 0xff) << 8)
                        | (hash[offset + 3] & 0xff);

            const string steamChars = "23456789BCDFGHJKMNPQRTVWXY";
            var code = "";
            for (int i = 0; i < 5; i++)
            {
                code += steamChars[codeInt % steamChars.Length];
                codeInt /= steamChars.Length;
            }
            return code;

        }
    }
}
