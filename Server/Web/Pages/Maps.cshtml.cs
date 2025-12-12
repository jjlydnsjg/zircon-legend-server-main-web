using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Envir;
using Library.SystemModels;
using Library;
using System.Collections.Generic;
using System.Linq;

namespace Server.Web.Pages
{
    [Authorize]
    public class MapsModel : PageModel
    {
        public List<MapViewModel> Maps { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Keyword { get; set; }

        public string? Message { get; set; }

        public void OnGet()
        {
            LoadMaps();
        }

        private void LoadMaps()
        {
            try
            {
                foreach (var kvp in SEnvir.Maps.ToList())
                {
                    var mapInfo = kvp.Key;
                    var map = kvp.Value;

                    if (!string.IsNullOrWhiteSpace(Keyword))
                    {
                        if (!(mapInfo.Description?.Contains(Keyword, System.StringComparison.OrdinalIgnoreCase) ?? false) &&
                            !(mapInfo.FileName?.Contains(Keyword, System.StringComparison.OrdinalIgnoreCase) ?? false))
                        {
                            continue;
                        }
                    }

                    Maps.Add(new MapViewModel
                    {
                        Index = mapInfo.Index,
                        FileName = mapInfo.FileName ?? "",
                        Description = mapInfo.Description ?? "",
                        MinimumLevel = mapInfo.MinimumLevel,
                        MaximumLevel = mapInfo.MaximumLevel,
                        PlayerCount = map.Players?.Count ?? 0,
                        MonsterCount = map.Objects?.Count(o => o is Zircon.Server.Models.MonsterObject) ?? 0,
                        Width = map.Width,
                        Height = map.Height,
                        AllowRT = !mapInfo.AllowRT ? "禁止" : "允许"
                    });
                }

                Maps = Maps.OrderByDescending(m => m.PlayerCount).ThenBy(m => m.Description).ToList();
            }
            catch
            {
                // Prevent enumeration errors
            }
        }

        public IActionResult OnPostTeleport(string playerName, int mapIndex, int x, int y)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                Message = "权限不足，需要 Admin 权限";
                LoadMaps();
                return Page();
            }

            try
            {
                var player = SEnvir.Players.FirstOrDefault(p =>
                    p?.Character?.CharacterName?.Equals(playerName, System.StringComparison.OrdinalIgnoreCase) == true);

                if (player == null)
                {
                    Message = $"玩家 {playerName} 不在线";
                    LoadMaps();
                    return Page();
                }

                var mapEntry = SEnvir.Maps.FirstOrDefault(m => m.Key.Index == mapIndex);
                if (mapEntry.Value == null)
                {
                    Message = $"地图 {mapIndex} 不存在";
                    LoadMaps();
                    return Page();
                }

                var targetMap = mapEntry.Value;
                var location = new System.Drawing.Point(x, y);

                // If x,y is 0,0, find a valid spawn point
                if (x <= 0 || y <= 0)
                {
                    location = targetMap.GetRandomLocation();
                }

                player.Teleport(targetMap, location);
                Message = $"已将 {playerName} 传送到 {mapEntry.Key.Description} ({location.X}, {location.Y})";
            }
            catch (System.Exception ex)
            {
                Message = $"传送失败: {ex.Message}";
            }

            LoadMaps();
            return Page();
        }

        public IActionResult OnPostBroadcast(string message)
        {
            if (!HasPermission(AccountIdentity.Operator))
            {
                Message = "权限不足，需要 Operator 权限";
                LoadMaps();
                return Page();
            }

            try
            {
                foreach (var player in SEnvir.Players.ToList())
                {
                    player?.Connection?.ReceiveChat($"[系统公告] {message}", Library.MessageType.Announcement);
                }
                Message = $"已发送全服公告: {message}";
            }
            catch (System.Exception ex)
            {
                Message = $"发送失败: {ex.Message}";
            }

            LoadMaps();
            return Page();
        }

        // 获取地图详情 (AJAX)
        public IActionResult OnGetMapDetail(int mapIndex)
        {
            if (!HasPermission(AccountIdentity.Admin))
            {
                return new JsonResult(new { success = false, message = "权限不足" });
            }

            try
            {
                var mapInfo = SEnvir.MapInfoList?.Binding?.FirstOrDefault(m => m.Index == mapIndex);
                if (mapInfo == null)
                {
                    return new JsonResult(new { success = false, message = "地图不存在" });
                }

                var detail = new MapDetailViewModel
                {
                    Index = mapInfo.Index,
                    FileName = mapInfo.FileName ?? "",
                    Description = mapInfo.Description ?? "",
                    MiniMap = mapInfo.MiniMap,
                    Light = (int)mapInfo.Light,
                    Fight = (int)mapInfo.Fight,
                    AllowRT = mapInfo.AllowRT,
                    AllowTT = mapInfo.AllowTT,
                    CanHorse = mapInfo.CanHorse,
                    CanMine = mapInfo.CanMine,
                    CanMarriageRecall = mapInfo.CanMarriageRecall,
                    AllowRecall = mapInfo.AllowRecall,
                    MinimumLevel = mapInfo.MinimumLevel,
                    MaximumLevel = mapInfo.MaximumLevel,
                    MonsterHealth = mapInfo.MonsterHealth,
                    MaxMonsterHealth = mapInfo.MaxMonsterHealth,
                    MonsterDamage = mapInfo.MonsterDamage,
                    MaxMonsterDamage = mapInfo.MaxMonsterDamage,
                    DropRate = mapInfo.DropRate,
                    MaxDropRate = mapInfo.MaxDropRate,
                    ExperienceRate = mapInfo.ExperienceRate,
                    MaxExperienceRate = mapInfo.MaxExperienceRate,
                    GoldRate = mapInfo.GoldRate,
                    MaxGoldRate = mapInfo.MaxGoldRate,
                    SkillDelay = mapInfo.SkillDelay
                };

                return new JsonResult(new { success = true, data = detail });
            }
            catch (System.Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // 新建地图
        public IActionResult OnPostCreateMap(
            string fileName,
            string description,
            int miniMap,
            int light,
            int fight,
            bool allowRT,
            bool allowTT,
            bool canHorse,
            bool canMine,
            bool canMarriageRecall,
            bool allowRecall,
            int minimumLevel,
            int maximumLevel,
            int monsterHealth,
            int maxMonsterHealth,
            int monsterDamage,
            int maxMonsterDamage,
            int dropRate,
            int maxDropRate,
            int experienceRate,
            int maxExperienceRate,
            int goldRate,
            int maxGoldRate,
            int skillDelay)
        {
            if (!HasPermission(AccountIdentity.SuperAdmin))
            {
                Message = "权限不足，需要 SuperAdmin 权限";
                LoadMaps();
                return Page();
            }

            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    Message = "地图文件名不能为空";
                    LoadMaps();
                    return Page();
                }

                var newMap = SEnvir.MapInfoList?.CreateNewObject();
                if (newMap == null)
                {
                    Message = "创建地图失败";
                    LoadMaps();
                    return Page();
                }

                newMap.FileName = fileName;
                newMap.Description = string.IsNullOrWhiteSpace(description) ? fileName : description;
                newMap.MiniMap = miniMap;
                newMap.Light = (LightSetting)light;
                newMap.Fight = (FightSetting)fight;
                newMap.AllowRT = allowRT;
                newMap.AllowTT = allowTT;
                newMap.CanHorse = canHorse;
                newMap.CanMine = canMine;
                newMap.CanMarriageRecall = canMarriageRecall;
                newMap.AllowRecall = allowRecall;
                newMap.MinimumLevel = minimumLevel;
                newMap.MaximumLevel = maximumLevel;
                newMap.MonsterHealth = monsterHealth;
                newMap.MaxMonsterHealth = maxMonsterHealth;
                newMap.MonsterDamage = monsterDamage;
                newMap.MaxMonsterDamage = maxMonsterDamage;
                newMap.DropRate = dropRate;
                newMap.MaxDropRate = maxDropRate;
                newMap.ExperienceRate = experienceRate;
                newMap.MaxExperienceRate = maxExperienceRate;
                newMap.GoldRate = goldRate;
                newMap.MaxGoldRate = maxGoldRate;
                newMap.SkillDelay = skillDelay;

                Message = $"地图 [{newMap.Index}] {description} 创建成功（需重启服务器加载地图文件）";
                SEnvir.Log($"[Admin] 新建地图: [{newMap.Index}] {description} ({fileName})");
            }
            catch (System.Exception ex)
            {
                Message = $"创建失败: {ex.Message}";
            }

            LoadMaps();
            return Page();
        }

        // 编辑地图
        public IActionResult OnPostUpdateMap(
            int mapIndex,
            string fileName,
            string description,
            int miniMap,
            int light,
            int fight,
            bool allowRT,
            bool allowTT,
            bool canHorse,
            bool canMine,
            bool canMarriageRecall,
            bool allowRecall,
            int minimumLevel,
            int maximumLevel,
            int monsterHealth,
            int maxMonsterHealth,
            int monsterDamage,
            int maxMonsterDamage,
            int dropRate,
            int maxDropRate,
            int experienceRate,
            int maxExperienceRate,
            int goldRate,
            int maxGoldRate,
            int skillDelay)
        {
            if (!HasPermission(AccountIdentity.SuperAdmin))
            {
                Message = "权限不足，需要 SuperAdmin 权限";
                LoadMaps();
                return Page();
            }

            try
            {
                var mapInfo = SEnvir.MapInfoList?.Binding?.FirstOrDefault(m => m.Index == mapIndex);
                if (mapInfo == null)
                {
                    Message = $"地图索引 {mapIndex} 不存在";
                    LoadMaps();
                    return Page();
                }

                var oldName = mapInfo.Description;

                mapInfo.FileName = fileName;
                mapInfo.Description = description;
                mapInfo.MiniMap = miniMap;
                mapInfo.Light = (LightSetting)light;
                mapInfo.Fight = (FightSetting)fight;
                mapInfo.AllowRT = allowRT;
                mapInfo.AllowTT = allowTT;
                mapInfo.CanHorse = canHorse;
                mapInfo.CanMine = canMine;
                mapInfo.CanMarriageRecall = canMarriageRecall;
                mapInfo.AllowRecall = allowRecall;
                mapInfo.MinimumLevel = minimumLevel;
                mapInfo.MaximumLevel = maximumLevel;
                mapInfo.MonsterHealth = monsterHealth;
                mapInfo.MaxMonsterHealth = maxMonsterHealth;
                mapInfo.MonsterDamage = monsterDamage;
                mapInfo.MaxMonsterDamage = maxMonsterDamage;
                mapInfo.DropRate = dropRate;
                mapInfo.MaxDropRate = maxDropRate;
                mapInfo.ExperienceRate = experienceRate;
                mapInfo.MaxExperienceRate = maxExperienceRate;
                mapInfo.GoldRate = goldRate;
                mapInfo.MaxGoldRate = maxGoldRate;
                mapInfo.SkillDelay = skillDelay;

                Message = $"地图 [{mapIndex}] {description} 已更新";
                SEnvir.Log($"[Admin] 修改地图: [{mapIndex}] {oldName} -> {description}");
            }
            catch (System.Exception ex)
            {
                Message = $"修改失败: {ex.Message}";
            }

            LoadMaps();
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

    public class MapViewModel
    {
        public int Index { get; set; }
        public string FileName { get; set; } = "";
        public string Description { get; set; } = "";
        public int MinimumLevel { get; set; }
        public int MaximumLevel { get; set; }
        public int PlayerCount { get; set; }
        public int MonsterCount { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AllowRT { get; set; } = "";
    }

    public class MapDetailViewModel
    {
        public int Index { get; set; }
        public string FileName { get; set; } = "";
        public string Description { get; set; } = "";
        public int MiniMap { get; set; }
        public int Light { get; set; }
        public int Fight { get; set; }
        public bool AllowRT { get; set; }
        public bool AllowTT { get; set; }
        public bool CanHorse { get; set; }
        public bool CanMine { get; set; }
        public bool CanMarriageRecall { get; set; }
        public bool AllowRecall { get; set; }
        public int MinimumLevel { get; set; }
        public int MaximumLevel { get; set; }
        public int MonsterHealth { get; set; }
        public int MaxMonsterHealth { get; set; }
        public int MonsterDamage { get; set; }
        public int MaxMonsterDamage { get; set; }
        public int DropRate { get; set; }
        public int MaxDropRate { get; set; }
        public int ExperienceRate { get; set; }
        public int MaxExperienceRate { get; set; }
        public int GoldRate { get; set; }
        public int MaxGoldRate { get; set; }
        public int SkillDelay { get; set; }
    }
}
