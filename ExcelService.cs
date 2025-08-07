using System;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;

namespace SteamAutoLogin
{
    /// <summary>
    /// Represents a single account read from the Excel file.
    /// </summary>
    public class AccountInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        /// <summary>
        /// Third column: whether the account has been upgraded (e.g. to prime).
        /// </summary>
        public string IsUpgraded { get; set; }
    }

    /// <summary>
    /// Utility for reading account information from an Excel workbook.
    /// </summary>
    public class ExcelService
    {
        /// <summary>
        /// Reads the accounts from the specified Excel file. The expected layout is:
        /// Username in column A, password in column B and upgrade status in column C.
        /// The first row is treated as a header and skipped.
        /// </summary>
        /// <param name="filePath">Relative or absolute path to an .xlsx file.</param>
        /// <returns>A list of <see cref="AccountInfo"/> objects.</returns>
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
                            continue; // skip header row
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