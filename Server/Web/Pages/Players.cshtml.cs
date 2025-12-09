using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Envir;
using Server.Web.Services;
using System.Collections.Generic;
using System.Linq;
using Library;
using Library.Network.GeneralPackets;

namespace Server.Web.Pages
{
    [Authorize]
    public class PlayersModel : PageModel
    {
        private readonly PlayerService _playerService;

        public PlayersModel(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public List<PlayerViewModel> Players { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Keyword { get; set; }

        public string? Message { get; set; }

        private bool IsAjaxRequest()
        {
            if (HttpContext?.Request?.Headers == null) return false;
            return HttpContext.Request.Headers.TryGetValue("X-Requested-With", out var values)
                   && values.Any(v => v.Equals("XMLHttpRequest", System.StringComparison.OrdinalIgnoreCase));
        }

        public void OnGet()
        {
            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                Players = _playerService.SearchPlayers(Keyword);
            }
            else
            {
                Players = _playerService.GetOnlinePlayers();
            }
        }

        public IActionResult OnPostRecall(string playerName)
        {
            try
            {
                var targetPlayer = _playerService.GetOnlinePlayer(playerName);
                if (targetPlayer == null)
                {
                    var result = new { success = false, message = $"玩家 {playerName} 不在线" };
                    if (IsAjaxRequest()) return new JsonResult(result);

                    Message = result.message;
                    OnGet();
                    return Page();
                }
                else
                {
                    // 获取当前管理员的角色（如果在线）
                    var adminEmail = User.Identity?.Name;
                    var adminPlayer = SEnvir.Players.FirstOrDefault(p =>
                        p?.Character?.Account?.EMailAddress?.Equals(adminEmail, System.StringComparison.OrdinalIgnoreCase) == true);

                    if (adminPlayer != null)
                    {
                        // 召唤到管理员身边
                        targetPlayer.Teleport(adminPlayer.CurrentMap, adminPlayer.CurrentLocation);
                        var result = new { success = true, message = $"已将 {playerName} 召唤到您身边" };
                        if (IsAjaxRequest()) return new JsonResult(result);

                        Message = result.message;
                        OnGet();
                        return Page();
                    }
                    else
                    {
                        var result = new { success = false, message = "您当前不在线，无法召唤玩家" };
                        if (IsAjaxRequest()) return new JsonResult(result);

                        Message = result.message;
                        OnGet();
                        return Page();
                    }
                }
            }
            catch (System.Exception ex)
            {
                var result = new { success = false, message = $"召唤失败: {ex.Message}" };
                if (IsAjaxRequest()) return new JsonResult(result);

                Message = result.message;
                OnGet();
                return Page();
            }
        }

        public IActionResult OnPostKick(string playerName)
        {
            try
            {
                var targetPlayer = _playerService.GetOnlinePlayer(playerName);
                if (targetPlayer == null)
                {
                    var result = new { success = false, message = $"玩家 {playerName} 不在线" };
                    if (IsAjaxRequest()) return new JsonResult(result);

                    Message = result.message;
                    OnGet();
                    return Page();
                }
                else
                {
                    // 使用 AnotherUserAdmin 作为踢下线原因
                    targetPlayer.Connection?.TrySendDisconnect(new Disconnect
                    {
                        Reason = DisconnectReason.AnotherUserAdmin
                    });
                    var result = new { success = true, message = $"已将 {playerName} 踢下线" };
                    if (IsAjaxRequest()) return new JsonResult(result);

                    Message = result.message;
                    OnGet();
                    return Page();
                }
            }
            catch (System.Exception ex)
            {
                var result = new { success = false, message = $"踢出失败: {ex.Message}" };
                if (IsAjaxRequest()) return new JsonResult(result);

                Message = result.message;
                OnGet();
                return Page();
            }
        }

        /// <summary>
        /// 提升玩家等级
        /// </summary>
        public IActionResult OnPostLevelUp(string playerName, int levels)
        {
            // 检查权限
            if (!HasPermission(AccountIdentity.Admin))
            {
                return new JsonResult(new { success = false, message = "权限不足，需要 Admin 权限" });
            }

            try
            {
                var targetPlayer = _playerService.GetOnlinePlayer(playerName);
                if (targetPlayer == null)
                {
                    return new JsonResult(new { success = false, message = $"玩家 {playerName} 不在线" });
                }

                if (levels <= 0)
                {
                    var result = new { success = false, message = "提升等级数必须大于0" };
                    if (IsAjaxRequest()) return new JsonResult(result);

                    Message = result.message;
                    OnGet();
                    return Page();
                }

                // 获取当前等级
                var currentLevel = targetPlayer.Level;
                var newLevel = currentLevel + levels;
                var maxLevel = Config.MaxLevel;

                // 限制最大等级（可以根据游戏设置调整）
                if (newLevel > maxLevel)
                {
                    var result = new { success = false, message = $"等级不能超过{maxLevel}（当前{currentLevel}级，尝试提升到{newLevel}级）" };
                    if (IsAjaxRequest()) return new JsonResult(result);

                    Message = result.message;
                    OnGet();
                    return Page();
                }

                // 提升等级
                targetPlayer.Level = newLevel;
                targetPlayer.LevelUp();

                var success = new
                {
                    success = true,
                    message = $"已将 {playerName} 从 {currentLevel} 级提升到 {targetPlayer.Level} 级",
                    newLevel = targetPlayer.Level
                };
                if (IsAjaxRequest()) return new JsonResult(success);

                Message = success.message;
                OnGet();
                return Page();
            }
            catch (System.Exception ex)
            {
                var result = new { success = false, message = $"提升等级失败: {ex.Message}" };
                if (IsAjaxRequest()) return new JsonResult(result);

                Message = result.message;
                OnGet();
                return Page();
            }
        }

        /// <summary>
        /// 检查权限
        /// </summary>
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
