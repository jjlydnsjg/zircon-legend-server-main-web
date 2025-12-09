using Server.DBModels;
using Server.Envir;
using System;
using System.Collections.Generic;
using System.Linq;
using Library;

namespace Server.Web.Services
{
    /// <summary>
    /// 账户管理服务
    /// </summary>
    public class AccountService
    {
        /// <summary>
        /// 获取所有账户（分页）
        /// </summary>
        public (List<AccountViewModel> Accounts, int TotalCount) GetAllAccounts(int page = 1, int pageSize = 10)
        {
            var accounts = new List<AccountViewModel>();
            int totalCount = 0;

            try
            {
                var allAccounts = SEnvir.AccountInfoList.Binding.ToList();
                totalCount = allAccounts.Count;

                foreach (var account in allAccounts.Skip((page - 1) * pageSize).Take(pageSize))
                {
                    accounts.Add(MapToViewModel(account));
                }
            }
            catch
            {
                // 防止数据库访问异常
            }

            return (accounts, totalCount);
        }

        /// <summary>
        /// 搜索账户（分页）
        /// </summary>
        public (List<AccountViewModel> Accounts, int TotalCount) SearchAccounts(string keyword, int page = 1, int pageSize = 10)
        {
            var accounts = new List<AccountViewModel>();
            int totalCount = 0;

            try
            {
                var query = SEnvir.AccountInfoList.Binding;

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(a =>
                        (a.EMailAddress != null && a.EMailAddress.Contains(keyword, System.StringComparison.OrdinalIgnoreCase)) ||
                        a.Characters.Any(c => c.CharacterName != null && c.CharacterName.Contains(keyword, System.StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }

                totalCount = query.Count;

                foreach (var account in query.Skip((page - 1) * pageSize).Take(pageSize))
                {
                    accounts.Add(MapToViewModel(account));
                }
            }
            catch
            {
                // 防止数据库访问异常
            }

            return (accounts, totalCount);
        }

        /// <summary>
        /// 获取账户详情
        /// </summary>
        public AccountViewModel? GetAccountByEmail(string email)
        {
            try
            {
                var account = FindAccountByEmail(email);
                if (account == null) return null;
                return MapToViewModel(account);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据邮箱查找账户
        /// </summary>
        private AccountInfo? FindAccountByEmail(string email)
        {
            if (SEnvir.AccountInfoList?.Binding == null) return null;

            for (int i = 0; i < SEnvir.AccountInfoList.Count; i++)
            {
                var account = SEnvir.AccountInfoList[i];
                if (string.Compare(account.EMailAddress, email, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return account;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取账户总数
        /// </summary>
        public int GetAccountCount()
        {
            try
            {
                return SEnvir.AccountInfoList.Binding.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 封禁账户
        /// </summary>
        public bool BanAccount(string email, bool banned)
        {
            try
            {
                var account = FindAccountByEmail(email);
                if (account == null) return false;

                account.Banned = banned;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 修改账户权限
        /// </summary>
        public bool SetPermission(string email, AccountIdentity permission)
        {
            try
            {
                var account = FindAccountByEmail(email);
                if (account == null) return false;

                account.Identify = permission;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private AccountViewModel MapToViewModel(AccountInfo account)
        {
            return new AccountViewModel
            {
                Email = account.EMailAddress ?? "",
                Permission = account.Identify.ToString(),
                PermissionValue = (int)account.Identify,
                GameGold = account.GameGold,
                HuntGold = account.HuntGold,
                Banned = account.Banned,
                CreatedDate = account.CreationDate,
                LastLogin = account.LastLogin,
                CharacterCount = account.Characters.Count,
                Characters = account.Characters.Select(c => new CharacterViewModel
                {
                    Name = c.CharacterName ?? "",
                    Class = c.Class.ToString(),
                    Level = c.Level,
                    Deleted = c.Deleted
                }).ToList()
            };
        }
    }

    /// <summary>
    /// 账户视图模型
    /// </summary>
    public class AccountViewModel
    {
        public string Email { get; set; } = "";
        public string Permission { get; set; } = "";
        public int PermissionValue { get; set; }
        public int GameGold { get; set; }
        public int HuntGold { get; set; }
        public bool Banned { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastLogin { get; set; }
        public int CharacterCount { get; set; }
        public List<CharacterViewModel> Characters { get; set; } = new();
    }

    /// <summary>
    /// 角色视图模型
    /// </summary>
    public class CharacterViewModel
    {
        public string Name { get; set; } = "";
        public string Class { get; set; } = "";
        public int Level { get; set; }
        public bool Deleted { get; set; }
    }
}
