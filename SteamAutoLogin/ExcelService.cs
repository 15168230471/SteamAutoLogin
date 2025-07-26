using System;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;

namespace SteamAutoLogin
{
    public class AccountInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string IsUpgraded { get; set; }  // 第三列：是否升级完成
    }

    public class ExcelService
    {
        public List<AccountInfo> ReadAccountsFromExcel(string filePath)
        {
            List<AccountInfo> accounts = new List<AccountInfo>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    bool isFirstRow = true;
                    while (reader.Read())
                    {
                        if (isFirstRow)
                        {
                            isFirstRow = false;
                            continue; // 跳过表头
                        }
                        var username = reader.GetString(0);
                        var password = reader.GetString(1);
                        var isUpgraded = reader.GetString(2);

                        if (!string.IsNullOrWhiteSpace(username) &&
                            !string.IsNullOrWhiteSpace(password) &&
                            !string.IsNullOrWhiteSpace(isUpgraded))
                        {
                            accounts.Add(new AccountInfo
                            {
                                Username = username,
                                Password = password,
                                IsUpgraded = isUpgraded
                            });
                        }
                    }
                }
            }
            return accounts;
        }

    }
}
