using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Envir;
using Server.DBModels;
using Library.SystemModels;
using Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Web.Pages
{
    [Authorize]
    public class SkillsModel : PageModel
    {
        public List<MagicViewModel> Magics { get; set; } = new();
        public int TotalCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Keyword { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ClassFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SchoolFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public int PageSize { get; set; } = 50;
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

        public string? Message { get; set; }

        public void OnGet()
        {
            LoadMagics();
        }

        private void LoadMagics()
        {
            try
            {
                if (SEnvir.MagicInfoList?.Binding == null) return;

                var query = SEnvir.MagicInfoList.Binding.AsEnumerable();

                // Keyword filter
                if (!string.IsNullOrWhiteSpace(Keyword))
                {
                    query = query.Where(m =>
                        (m.Name?.Contains(Keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        m.Magic.ToString().Contains(Keyword, StringComparison.OrdinalIgnoreCase) ||
                        m.Index.ToString().Contains(Keyword));
                }

                // Class filter
                if (!string.IsNullOrWhiteSpace(ClassFilter) && Enum.TryParse<MirClass>(ClassFilter, out var mirClass))
                {
                    query = query.Where(m => m.Class == mirClass);
                }

                // School filter
                if (!string.IsNullOrWhiteSpace(SchoolFilter) && Enum.TryParse<MagicSchool>(SchoolFilter, out var school))
                {
                    query = query.Where(m => m.School == school);
                }

                TotalCount = query.Count();

                Magics = query
                    .OrderBy(m => m.Class)
                    .ThenBy(m => m.School)
                    .ThenBy(m => m.NeedLevel1)
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .Select(m => new MagicViewModel
                    {
                        Index = m.Index,
                        Name = m.Name ?? "Unknown",
                        Magic = m.Magic.ToString(),
                        Class = m.Class.ToString(),
                        School = m.School.ToString(),
                        Mode = m.Mode.ToString(),
                        Icon = m.Icon,
                        MinBasePower = m.MinBasePower,
                        MaxBasePower = m.MaxBasePower,
                        MinLevelPower = m.MinLevelPower,
                        MaxLevelPower = m.MaxLevelPower,
                        BaseCost = m.BaseCost,
                        LevelCost = m.LevelCost,
                        NeedLevel1 = m.NeedLevel1,
                        NeedLevel2 = m.NeedLevel2,
                        NeedLevel3 = m.NeedLevel3,
                        Experience1 = m.Experience1,
                        Experience2 = m.Experience2,
                        Experience3 = m.Experience3,
                        Delay = m.Delay,
                        Description = m.Description ?? ""
                    })
                    .ToList();
            }
            catch
            {
                // Prevent enumeration errors
            }
        }

        public IActionResult OnPostGiveSkill(string playerName, int magicIndex, int level)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                LoadMagics();
                return Page();
            }

            try
            {
                var player = SEnvir.Players.FirstOrDefault(p =>
                    p?.Character?.CharacterName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

                if (player == null)
                {
                    Message = $"玩家 {playerName} 不在线";
                    LoadMagics();
                    return Page();
                }

                var magicInfo = SEnvir.MagicInfoList?.Binding?.FirstOrDefault(m => m.Index == magicIndex);
                if (magicInfo == null)
                {
                    Message = $"技能 ID {magicIndex} 不存在";
                    LoadMagics();
                    return Page();
                }

                // Check if player already has this skill
                var existingMagic = player.Character.Magics.FirstOrDefault(m => m.Info?.Index == magicIndex);
                if (existingMagic != null)
                {
                    // Update existing skill level
                    existingMagic.Level = Math.Max(0, Math.Min(level, Config.技能最高等级));
                    existingMagic.Experience = 0;

                    // Notify player
                    player.Enqueue(new Library.Network.ServerPackets.MagicLeveled
                    {
                        InfoIndex = magicInfo.Index,
                        Level = existingMagic.Level,
                        Experience = existingMagic.Experience
                    });

                    Message = $"已更新玩家 {playerName} 的技能 [{magicInfo.Name}] 等级为 {existingMagic.Level}";
                    SEnvir.Log($"[Admin] 更新技能: {playerName} - {magicInfo.Name} Lv.{existingMagic.Level}");
                }
                else
                {
                    // Create new skill
                    var userMagic = SEnvir.UserMagicList.CreateNewObject();
                    userMagic.Character = player.Character;
                    userMagic.Info = magicInfo;
                    userMagic.Level = Math.Max(0, Math.Min(level, Config.技能最高等级));
                    userMagic.Experience = 0;

                    // Add to player's magic list (Character.Magics is a DBModel list, player.Magics is MagicType dictionary)
                    player.Character.Magics.Add(userMagic);
                    player.Magics[magicInfo.Magic] = userMagic;

                    // Notify player
                    player.Enqueue(new Library.Network.ServerPackets.NewMagic { Magic = userMagic.ToClientInfo() });

                    Message = $"已给玩家 {playerName} 添加技能 [{magicInfo.Name}] Lv.{userMagic.Level}";
                    SEnvir.Log($"[Admin] 添加技能: {playerName} - {magicInfo.Name} Lv.{userMagic.Level}");
                }
            }
            catch (Exception ex)
            {
                Message = $"操作失败: {ex.Message}";
            }

            LoadMagics();
            return Page();
        }

        public IActionResult OnPostRemoveSkill(string playerName, int magicIndex)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                LoadMagics();
                return Page();
            }

            try
            {
                var player = SEnvir.Players.FirstOrDefault(p =>
                    p?.Character?.CharacterName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

                if (player == null)
                {
                    Message = $"玩家 {playerName} 不在线";
                    LoadMagics();
                    return Page();
                }

                var userMagic = player.Character.Magics.FirstOrDefault(m => m.Info?.Index == magicIndex);
                if (userMagic == null)
                {
                    Message = $"玩家 {playerName} 没有该技能";
                    LoadMagics();
                    return Page();
                }

                var magicName = userMagic.Info?.Name ?? "Unknown";
                var magicType = userMagic.Info?.Magic;

                // Remove from player (player.Magics is Dictionary<MagicType, UserMagic>)
                if (magicType.HasValue)
                {
                    player.Magics.Remove(magicType.Value);
                }
                player.Character.Magics.Remove(userMagic);

                // Delete from database
                userMagic.Delete();

                // Note: No RemoveMagic packet available, player needs to relog to see changes
                Message = $"已移除玩家 {playerName} 的技能 [{magicName}]（玩家需重新登录生效）";
                SEnvir.Log($"[Admin] 移除技能: {playerName} - {magicName}");
            }
            catch (Exception ex)
            {
                Message = $"操作失败: {ex.Message}";
            }

            LoadMagics();
            return Page();
        }

        public IActionResult OnPostUpdateMagic(
            int index, string name, int icon,
            int minBasePower, int maxBasePower,
            int minLevelPower, int maxLevelPower,
            int baseCost, int levelCost,
            int needLevel1, int needLevel2, int needLevel3,
            int experience1, int experience2, int experience3,
            int delay, string description)
        {
            if (!HasPermission(AccountIdentity.SuperAdmin))
            {
                Message = "权限不足，需要 SuperAdmin 权限";
                LoadMagics();
                return Page();
            }

            try
            {
                var magicInfo = SEnvir.MagicInfoList?.Binding?.FirstOrDefault(m => m.Index == index);
                if (magicInfo == null)
                {
                    Message = $"技能 ID {index} 不存在";
                    LoadMagics();
                    return Page();
                }

                // Update properties
                magicInfo.Name = name;
                magicInfo.Icon = icon;
                magicInfo.MinBasePower = minBasePower;
                magicInfo.MaxBasePower = maxBasePower;
                magicInfo.MinLevelPower = minLevelPower;
                magicInfo.MaxLevelPower = maxLevelPower;
                magicInfo.BaseCost = baseCost;
                magicInfo.LevelCost = levelCost;
                magicInfo.NeedLevel1 = needLevel1;
                magicInfo.NeedLevel2 = needLevel2;
                magicInfo.NeedLevel3 = needLevel3;
                magicInfo.Experience1 = experience1;
                magicInfo.Experience2 = experience2;
                magicInfo.Experience3 = experience3;
                magicInfo.Delay = delay;
                magicInfo.Description = description;

                Message = $"技能 [{name}] 已更新";
                SEnvir.Log($"[Admin] 更新技能属性: {name} (ID: {index})");
            }
            catch (Exception ex)
            {
                Message = $"更新失败: {ex.Message}";
            }

            LoadMagics();
            return Page();
        }

        public IActionResult OnPostGiveAllSkills(string playerName, int level)
        {
            if (!HasPermission(AccountIdentity.SuperAdmin))
            {
                Message = "权限不足，需要 SuperAdmin 权限";
                LoadMagics();
                return Page();
            }

            try
            {
                var player = SEnvir.Players.FirstOrDefault(p =>
                    p?.Character?.CharacterName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

                if (player == null)
                {
                    Message = $"玩家 {playerName} 不在线";
                    LoadMagics();
                    return Page();
                }

                var playerClass = player.Character.Class;
                int addedCount = 0;
                int updatedCount = 0;

                // Get all skills for player's class
                var classSkills = SEnvir.MagicInfoList?.Binding?
                    .Where(m => m.Class == playerClass)
                    .ToList() ?? new List<MagicInfo>();

                foreach (var magicInfo in classSkills)
                {
                    var existingMagic = player.Character.Magics.FirstOrDefault(m => m.Info?.Index == magicInfo.Index);

                    if (existingMagic != null)
                    {
                        // Update existing
                        existingMagic.Level = Math.Max(0, Math.Min(level, Config.技能最高等级));
                        existingMagic.Experience = 0;

                        player.Enqueue(new Library.Network.ServerPackets.MagicLeveled
                        {
                            InfoIndex = magicInfo.Index,
                            Level = existingMagic.Level,
                            Experience = existingMagic.Experience
                        });
                        updatedCount++;
                    }
                    else
                    {
                        // Create new
                        var userMagic = SEnvir.UserMagicList.CreateNewObject();
                        userMagic.Character = player.Character;
                        userMagic.Info = magicInfo;
                        userMagic.Level = Math.Max(0, Math.Min(level, Config.技能最高等级));
                        userMagic.Experience = 0;

                        player.Character.Magics.Add(userMagic);
                        player.Magics[magicInfo.Magic] = userMagic;

                        player.Enqueue(new Library.Network.ServerPackets.NewMagic { Magic = userMagic.ToClientInfo() });
                        addedCount++;
                    }
                }

                Message = $"已给玩家 {playerName} 添加 {addedCount} 个技能，更新 {updatedCount} 个技能到 Lv.{level}";
                SEnvir.Log($"[Admin] 全技能: {playerName} - 新增 {addedCount}, 更新 {updatedCount}, 等级 {level}");
            }
            catch (Exception ex)
            {
                Message = $"操作失败: {ex.Message}";
            }

            LoadMagics();
            return Page();
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

    public class MagicViewModel
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Magic { get; set; } = "";
        public string Class { get; set; } = "";
        public string School { get; set; } = "";
        public string Mode { get; set; } = "";
        public int Icon { get; set; }
        public int MinBasePower { get; set; }
        public int MaxBasePower { get; set; }
        public int MinLevelPower { get; set; }
        public int MaxLevelPower { get; set; }
        public int BaseCost { get; set; }
        public int LevelCost { get; set; }
        public int NeedLevel1 { get; set; }
        public int NeedLevel2 { get; set; }
        public int NeedLevel3 { get; set; }
        public int Experience1 { get; set; }
        public int Experience2 { get; set; }
        public int Experience3 { get; set; }
        public int Delay { get; set; }
        public string Description { get; set; } = "";
    }
}
