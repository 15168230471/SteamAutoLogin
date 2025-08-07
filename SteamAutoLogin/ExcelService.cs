using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;  // 需要加上这个
using ExcelDataReader;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace SteamAutoLogin
{
    public class AccountInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string IsUpgraded { get; set; }
        public int InitialLevel { get; set; }
        public int LastLevel { get; set; }
        public int CurrentLevel { get; set; }
    }

    public class ExcelService
    {

        public List<AccountInfo> ReadAccountsFromExcel(string filePath)
        {
            var accounts = new List<AccountInfo>();
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    bool isFirstRow = true;
                    while (reader.Read())
                    {
                        if (isFirstRow) { isFirstRow = false; continue; }
                        var username = reader.GetString(0);
                        var password = reader.GetString(1);
                        var isUpgraded = reader.GetString(2);
                        var initialLevel = reader.FieldCount > 3 && !reader.IsDBNull(3) ? Convert.ToInt32(reader.GetDouble(3)) : 0;
                        var lastLevel = reader.FieldCount > 4 && !reader.IsDBNull(4) ? Convert.ToInt32(reader.GetDouble(4)) : 0;
                        var currentLevel = reader.FieldCount > 5 && !reader.IsDBNull(5) ? Convert.ToInt32(reader.GetDouble(5)) : 0;

                        if (!string.IsNullOrWhiteSpace(username))
                        {
                            accounts.Add(new AccountInfo
                            {
                                Username = username,
                                Password = password,
                                IsUpgraded = isUpgraded,
                                InitialLevel = initialLevel,
                                LastLevel = lastLevel,
                                CurrentLevel = currentLevel
                            });
                        }
                    }
                }
            }
            return accounts;
        }
   
    }
}
