using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Envir;
using Server.Web.Services;
using System;
using System.Collections.Generic;
using Library;

namespace Server.Web.Pages
{
    [Authorize]
    public class AccountsModel : PageModel
    {
        private readonly AccountService _accountService;
        private readonly AdminAuthService _authService;

        public AccountsModel(AccountService accountService, AdminAuthService authService)
        {
            _accountService = accountService;
            _authService = authService;
        }

        // 权限等级列表（用于下拉选择）
        public List<(string Name, int Value)> PermissionLevels { get; } = new()
        {
            ("Normal - 普通玩家", 0),
            ("Supervisor - 监督者", 1),
            ("Operator - 运营者", 2),
            ("Admin - 管理员", 3),
            ("SuperAdmin - 超级管理员", 4)
        };

        public List<AccountViewModel> Accounts { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        [BindProperty(SupportsGet = true)]
        public string? Keyword { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public string? Message { get; set; }

        public void OnGet()
        {
            CurrentPage = PageNumber > 0 ? PageNumber : 1;

            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                // 搜索模式
                var result = _accountService.SearchAccounts(Keyword, CurrentPage, PageSize);
                Accounts = result.Accounts;
                TotalCount = result.TotalCount;
            }
            else
            {
                // 默认显示所有账号（分页）
                var result = _accountService.GetAllAccounts(CurrentPage, PageSize);
                Accounts = result.Accounts;
                TotalCount = result.TotalCount;
            }
        }

        public IActionResult OnPostBan(string email)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                OnGet();
                return Page();
            }

            if (_accountService.BanAccount(email, true))
            {
                Message = $"账户 {email} 已被封禁";
            }
            else
            {
                Message = $"封禁失败，账户 {email} 不存在";
            }

            Keyword = email;
            OnGet();
            return Page();
        }

        public IActionResult OnPostUnban(string email)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                OnGet();
                return Page();
            }

            if (_accountService.BanAccount(email, false))
            {
                Message = $"账户 {email} 已解除封禁";
            }
            else
            {
                Message = $"解封失败，账户 {email} 不存在";
            }

            Keyword = email;
            OnGet();
            return Page();
        }

        public IActionResult OnPostModifyGold(string email, int amount)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                OnGet();
                return Page();
            }

            try
            {
                var account = FindAccountByEmail(email);
                if (account == null)
                {
                    Message = $"账户 {email} 不存在";
                }
                else
                {
                    var newGold = account.GameGold + amount;
                    if (newGold < 0) newGold = 0;

                    account.GameGold = newGold;
                    Message = $"账户 {email} 元宝已修改: {(amount >= 0 ? "+" : "")}{amount}，当前: {newGold}";
                }
            }
            catch (System.Exception ex)
            {
                Message = $"修改失败: {ex.Message}";
            }

            Keyword = email;
            OnGet();
            return Page();
        }

        /// <summary>
        /// 修改账户权限
        /// </summary>
        public IActionResult OnPostSetPermission(string email, int permission)
        {
            // 只有 SuperAdmin 才能修改权限
            if (!HasPermission(AccountIdentity.SuperAdmin))
            {
                Message = "权限不足，需要 SuperAdmin 权限才能修改账户权限";
                OnGet();
                return Page();
            }

            // 验证权限值范围
            if (permission < 0 || permission > 4)
            {
                Message = "无效的权限等级";
                OnGet();
                return Page();
            }

            var newPermission = (AccountIdentity)permission;
            var (success, message) = _authService.SetAccountPermission(email, newPermission);

            Message = message;
            Keyword = email;
            OnGet();
            return Page();
        }

        private Server.DBModels.AccountInfo? FindAccountByEmail(string email)
        {
            if (SEnvir.AccountInfoList?.Binding == null) return null;

            for (int i = 0; i < SEnvir.AccountInfoList.Count; i++)
            {
                var account = SEnvir.AccountInfoList[i];
                if (string.Compare(account.EMailAddress, email, System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return account;
                }
            }
            return null;
        }

        private bool HasPermission(AccountIdentity required)
        {
            var permissionClaim = User.FindFirst("Permission")?.Value;
            if (string.IsNullOrEmpty(permissionClaim)) return false;

            if (int.TryParse(permissionClaim, out int permValue))
            {
                return permValue >= (int)required;
            }
            return false;
        }
    }
}
