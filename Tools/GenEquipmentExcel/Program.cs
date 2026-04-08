using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

string outputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
    "..", "..", "..", "..", "..", "common", "excel", "xls", "equipment.xlsx"));
string legacyOutputPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
    "..", "..", "..", "..", "..", "common", "excel", "xls", "equipment_new.xlsx"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

using var pkg = new ExcelPackage(new FileInfo(outputPath));

// 辅助方法：获取或创建Sheet
ExcelWorksheet GetOrAddSheet(string sheetName)
{
    var ws = pkg.Workbook.Worksheets[sheetName];
    if (ws != null) return ws;
    return pkg.Workbook.Worksheets.Add(sheetName);
}

// 辅助方法：设置Sheet表头（每次重置内容）
void SetupSheet(string sheetName, string configFile, string pbFile, string scheme, string[] headers)
{
    var ws = GetOrAddSheet(sheetName);
    ws.Cells.Clear();

    ws.Cells[1, 1].Value = $"convert({configFile},{pbFile},{scheme})";
    ws.Cells[1, 1].Style.Font.Bold = true;
    ws.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
    ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 217, 217));

    for (int i = 0; i < headers.Length; i++)
    {
        var cell = ws.Cells[2, i + 1];
        cell.Value = headers[i];
        cell.Style.Font.Bold = true;
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(189, 215, 238));
        cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
    }
}

// 辅助方法：写入一行数据
void WriteRow(ExcelWorksheet ws, int row, object[] values)
{
    for (int i = 0; i < values.Length; i++)
        ws.Cells[row, i + 1].Value = values[i];
}

// 辅助方法：写入多行数据并自动列宽
void WriteRows(ExcelWorksheet ws, object[][] rows)
{
    for (int i = 0; i < rows.Length; i++)
        WriteRow(ws, i + 3, rows[i]);
    ws.Cells[ws.Dimension.Address].AutoFitColumns();
}

// ===== Sheet 1: 装备词条属性 =====
SetupSheet("装备词条属性", "equipment.txt", "EquipmentValueConf.pb", "EquipmentValueConf",
    new[] { "EquipmentValueId", "EquipmentValueDesc", "isPrefix" });

// ===== Sheet 2: 装备词条等阶 =====
SetupSheet("装备词条等阶", "equipment.txt", "EquipmentValueLevelConf.pb", "EquipmentValueLevelConf",
    new[] { "EquipmentValueLevelId", "EquipmentFloatMinValue", "EquipmentFloatMaxValue", "EquipmentValueName" });

// ===== Sheet 3: 装备势力 =====
SetupSheet("装备势力", "equipment.txt", "EquipmentForceConf.pb", "EquipmentForceConf",
    new[] { "EquipmentForceId", "EquipmentForceAtlas", "EquipmentForceSprite" });

// ===== Sheet 4: 装备小类 =====
SetupSheet("装备小类", "equipment.txt", "EquipmentTypeConf.pb", "EquipmentTypeConf",
    new[] { "EquipmentTypeId", "EquipmentBaseType", "EquipmentTypeName" });

// ===== Sheet 5: 装备基底属性（EquipmentBaseValueConf）=====
// 记录每种装备基底的基础属性范围（如物理伤害、护甲值等）
// EquipmentBaseValueId 与 EquipmentDetailTypeConf.EquipmentBaseValueId 对应
{
    SetupSheet("装备基底", "equipment.txt", "EquipmentBaseValueConf.pb", "EquipmentBaseValueConf",
        new[] { "EquipmentBaseValueId", "EquipmentBaseMinValue", "EquipmentBaseMaxValue", "EquipmentBaseValueDesc" });
    var ws = pkg.Workbook.Worksheets["装备基底"]!;
    // 武器基底属性为物理伤害范围，防具为护甲/闪避/能量护盾值
    // ID规则：与EquipmentDetailTypeConf的EquipmentBaseValueId一一对应
    WriteRows(ws, new object[][]
    {
        // ===== 爪 (SubCategoryId=1) =====
        new object[] {  1,  5,  8,  "物理伤害" }, // 虎爪
        new object[] {  2,  6,  9,  "物理伤害" }, // 猫爪
        new object[] {  3,  8, 14,  "物理伤害" }, // 鸟爪
        new object[] {  4, 11, 20,  "物理伤害" }, // 双爪
        new object[] {  5, 14, 27,  "物理伤害" }, // 尖爪
        new object[] {  6, 18, 32,  "物理伤害" }, // 战爪
        new object[] {  7, 22, 42,  "物理伤害" }, // 毒爪
        new object[] {  8, 26, 48,  "物理伤害" }, // 幻爪
        new object[] {  9, 35, 65,  "物理伤害" }, // 混沌爪
        // ===== 匕首 (SubCategoryId=2) =====
        new object[] { 10,  5,  8,  "物理伤害" }, // 粗糙匕首
        new object[] { 11,  6, 13,  "物理伤害" }, // 刺刀
        new object[] { 12, 10, 15,  "物理伤害" }, // 铁匕首
        new object[] { 13, 14, 21,  "物理伤害" }, // 幽灵刃
        new object[] { 14, 17, 32,  "物理伤害" }, // 骨刃
        new object[] { 15, 23, 42,  "物理伤害" }, // 暗影刃
        new object[] { 16, 26, 48,  "物理伤害" }, // 符文匕首
        new object[] { 17, 31, 58,  "物理伤害" }, // 混沌匕首
        // ===== 魔杖 (SubCategoryId=3) =====
        new object[] { 18,  4,  8,  "物理伤害" }, // 扭曲魔杖
        new object[] { 19,  5, 11,  "物理伤害" }, // 原始魔杖
        new object[] { 20,  7, 14,  "物理伤害" }, // 大魔杖
        new object[] { 21, 10, 19,  "物理伤害" }, // 水晶魔杖
        new object[] { 22, 14, 27,  "物理伤害" }, // 符文魔杖
        new object[] { 23, 19, 37,  "物理伤害" }, // 奥术魔杖
        new object[] { 24, 24, 45,  "物理伤害" }, // 神圣魔杖
        new object[] { 25, 30, 56,  "物理伤害" }, // 混沌魔杖
        // ===== 单手剑 (SubCategoryId=4) =====
        new object[] { 26,  6, 13,  "物理伤害" }, // 铁剑
        new object[] { 27,  9, 17,  "物理伤害" }, // 宽刃剑
        new object[] { 28, 12, 22,  "物理伤害" }, // 战剑
        new object[] { 29, 16, 30,  "物理伤害" }, // 古代剑
        new object[] { 30, 21, 39,  "物理伤害" }, // 精钢剑
        new object[] { 31, 27, 50,  "物理伤害" }, // 骑士剑
        new object[] { 32, 34, 64,  "物理伤害" }, // 混沌剑
        // ===== 单手斧 (SubCategoryId=5) =====
        new object[] { 33,  7, 14,  "物理伤害" }, // 石斧
        new object[] { 34, 10, 19,  "物理伤害" }, // 战斧
        new object[] { 35, 14, 26,  "物理伤害" }, // 裂颅斧
        new object[] { 36, 19, 36,  "物理伤害" }, // 精钢斧
        new object[] { 37, 25, 46,  "物理伤害" }, // 屠夫斧
        new object[] { 38, 32, 60,  "物理伤害" }, // 混沌斧
        // ===== 单手锤 (SubCategoryId=6) =====
        new object[] { 39,  6, 13,  "物理伤害" }, // 木锤
        new object[] { 40,  9, 18,  "物理伤害" }, // 石锤
        new object[] { 41, 13, 25,  "物理伤害" }, // 战锤
        new object[] { 42, 18, 34,  "物理伤害" }, // 精钢锤
        new object[] { 43, 24, 45,  "物理伤害" }, // 破碎锤
        new object[] { 44, 31, 58,  "物理伤害" }, // 混沌锤
        // ===== 权杖 (SubCategoryId=7) =====
        new object[] { 45,  5, 10,  "物理伤害" }, // 木权杖
        new object[] { 46,  8, 15,  "物理伤害" }, // 铁权杖
        new object[] { 47, 11, 22,  "物理伤害" }, // 战权杖
        new object[] { 48, 16, 30,  "物理伤害" }, // 精钢权杖
        new object[] { 49, 22, 41,  "物理伤害" }, // 混沌权杖
        // ===== 弓 (SubCategoryId=8) =====
        new object[] { 50,  5, 10,  "物理伤害" }, // 木弓
        new object[] { 51,  8, 15,  "物理伤害" }, // 短弓
        new object[] { 52, 12, 21,  "物理伤害" }, // 长弓
        new object[] { 53, 16, 30,  "物理伤害" }, // 复合弓
        new object[] { 54, 22, 41,  "物理伤害" }, // 战弓
        new object[] { 55, 29, 54,  "物理伤害" }, // 精钢弓
        new object[] { 56, 37, 70,  "物理伤害" }, // 混沌弓
        // ===== 双手剑 (SubCategoryId=9) =====
        new object[] { 57, 11, 22,  "物理伤害" }, // 双手铁剑
        new object[] { 58, 16, 31,  "物理伤害" }, // 双手战剑
        new object[] { 59, 22, 42,  "物理伤害" }, // 双手古代剑
        new object[] { 60, 30, 57,  "物理伤害" }, // 双手精钢剑
        new object[] { 61, 40, 76,  "物理伤害" }, // 双手混沌剑
        // ===== 双手斧 (SubCategoryId=10) =====
        new object[] { 62, 13, 24,  "物理伤害" }, // 双手石斧
        new object[] { 63, 18, 34,  "物理伤害" }, // 双手战斧
        new object[] { 64, 25, 47,  "物理伤害" }, // 双手裂颅斧
        new object[] { 65, 34, 64,  "物理伤害" }, // 双手精钢斧
        new object[] { 66, 45, 85,  "物理伤害" }, // 双手混沌斧
        // ===== 双手锤 (SubCategoryId=11) =====
        new object[] { 67, 12, 22,  "物理伤害" }, // 双手木锤
        new object[] { 68, 17, 32,  "物理伤害" }, // 双手石锤
        new object[] { 69, 24, 45,  "物理伤害" }, // 双手战锤
        new object[] { 70, 32, 60,  "物理伤害" }, // 双手精钢锤
        new object[] { 71, 43, 80,  "物理伤害" }, // 双手混沌锤
        // ===== 长矛 (SubCategoryId=12) =====
        new object[] { 72, 10, 20,  "物理伤害" }, // 木矛
        new object[] { 73, 15, 29,  "物理伤害" }, // 铁矛
        new object[] { 74, 21, 40,  "物理伤害" }, // 战矛
        new object[] { 75, 29, 55,  "物理伤害" }, // 精钢矛
        new object[] { 76, 39, 73,  "物理伤害" }, // 混沌矛
        // ===== 法杖 (SubCategoryId=13) =====
        new object[] { 77,  8, 15,  "物理伤害" }, // 木法杖
        new object[] { 78, 12, 22,  "物理伤害" }, // 铁法杖
        new object[] { 79, 17, 32,  "物理伤害" }, // 战法杖
        new object[] { 80, 24, 45,  "物理伤害" }, // 精钢法杖
        new object[] { 81, 32, 60,  "物理伤害" }, // 混沌法杖
        // ===== 头盔 (SubCategoryId=14) =====
        new object[] { 82, 10,  0,  "护甲" }, // 铁帽
        new object[] { 83, 16,  0,  "护甲" }, // 战盔
        new object[] { 84, 26,  0,  "护甲" }, // 大盔
        new object[] { 85, 40,  0,  "护甲" }, // 精钢盔
        new object[] { 86, 10,  0,  "闪避" }, // 皮帽
        new object[] { 87, 16,  0,  "闪避" }, // 皮盔
        new object[] { 88, 26,  0,  "闪避" }, // 猎人帽
        new object[] { 89, 40,  0,  "闪避" }, // 精钢皮盔
        new object[] { 90, 10,  0,  "能量护盾" }, // 布帽
        new object[] { 91, 16,  0,  "能量护盾" }, // 法师帽
        new object[] { 92, 26,  0,  "能量护盾" }, // 大法师帽
        new object[] { 93, 40,  0,  "能量护盾" }, // 精钢法师帽
        // ===== 胸甲 (SubCategoryId=15) =====
        new object[] { 94, 20,  0,  "护甲" }, // 铁甲
        new object[] { 95, 35,  0,  "护甲" }, // 战甲
        new object[] { 96, 55,  0,  "护甲" }, // 板甲
        new object[] { 97, 80,  0,  "护甲" }, // 精钢甲
        new object[] { 98, 20,  0,  "闪避" }, // 皮甲
        new object[] { 99, 35,  0,  "闪避" }, // 猎人甲
        new object[] {100, 55,  0,  "闪避" }, // 精钢皮甲
        new object[] {101, 20,  0,  "能量护盾" }, // 布甲
        new object[] {102, 35,  0,  "能量护盾" }, // 法师甲
        new object[] {103, 55,  0,  "能量护盾" }, // 精钢法师甲
        // ===== 手套 (SubCategoryId=16) =====
        new object[] {104,  8,  0,  "护甲" }, // 铁手套
        new object[] {105, 14,  0,  "护甲" }, // 战手套
        new object[] {106, 22,  0,  "护甲" }, // 精钢手套
        new object[] {107,  8,  0,  "闪避" }, // 皮手套
        new object[] {108, 14,  0,  "闪避" }, // 猎人手套
        new object[] {109, 22,  0,  "闪避" }, // 精钢皮手套
        new object[] {110,  8,  0,  "能量护盾" }, // 布手套
        new object[] {111, 14,  0,  "能量护盾" }, // 法师手套
        new object[] {112, 22,  0,  "能量护盾" }, // 精钢法师手套
        // ===== 鞋子 (SubCategoryId=17) =====
        new object[] {113,  8,  0,  "护甲" }, // 铁靴
        new object[] {114, 14,  0,  "护甲" }, // 战靴
        new object[] {115, 22,  0,  "护甲" }, // 精钢靴
        new object[] {116,  8,  0,  "闪避" }, // 皮靴
        new object[] {117, 14,  0,  "闪避" }, // 猎人靴
        new object[] {118, 22,  0,  "闪避" }, // 精钢皮靴
        new object[] {119,  8,  0,  "能量护盾" }, // 布靴
        new object[] {120, 14,  0,  "能量护盾" }, // 法师靴
        new object[] {121, 22,  0,  "能量护盾" }, // 精钢法师靴
        // ===== 腰带 (SubCategoryId=18) =====
        new object[] {122,  0,  0,  "无" }, // 皮腰带
        new object[] {123,  0,  0,  "无" }, // 重型腰带
        new object[] {124,  0,  0,  "无" }, // 腰带
        new object[] {125,  0,  0,  "无" }, // 绳腰带
        // ===== 戒指 (SubCategoryId=19) =====
        new object[] {134,  0,  0,  "无" }, // 铁戒指
        new object[] {135,  0,  0,  "无" }, // 金戒指
        new object[] {136,  0,  0,  "无" }, // 宝石戒指
        // ===== 项链 (SubCategoryId=20) =====
        new object[] {137,  0,  0,  "无" }, // 铁项链
        new object[] {138,  0,  0,  "无" }, // 金项链
        new object[] {139,  0,  0,  "无" }, // 宝石项链
        // ===== 盾牌 (SubCategoryId=21) =====
        new object[] {126, 20,  0,  "格挡" }, // 木盾
        new object[] {127, 35,  0,  "格挡" }, // 铁盾
        new object[] {128, 55,  0,  "格挡" }, // 战盾
        new object[] {129, 80,  0,  "格挡" }, // 精钓盾
        // ===== 箭袋 (SubCategoryId=22) =====
        new object[] {130,  0,  0,  "无" }, // 箭袋
        new object[] {131,  0,  0,  "无" }, // 精钓箭袋
        // ===== 法器 (SubCategoryId=23) =====
        new object[] {132,  0,  0,  "无" }, // 法球
        new object[] {133,  0,  0,  "无" }, // 精钓法球
        // ===== 戒指 (SubCategoryId=19) =====
        new object[] {134,  0,  0,  "无" }, // 铁戒指
        new object[] {135,  0,  0,  "无" }, // 金戒指
        new object[] {136,  0,  0,  "无" }, // 宝石戒指
        // ===== 项链 (SubCategoryId=20) =====
        new object[] {137,  0,  0,  "无" }, // 铁项链
        new object[] {138,  0,  0,  "无" }, // 金项链
        new object[] {139,  0,  0,  "无" }, // 宝石项链
    });
}

// ===== Sheet 6: 装备大类 =====
SetupSheet("装备大类", "equipment.txt", "EquipmentBaseTypeConf.pb", "EquipmentBaseTypeConf",
    new[] { "EquipmentBaseTypeId", "EquipmentBaseTypeName" });

// ===== Sheet 7: 装备插槽 =====
SetupSheet("装备插槽", "equipment.txt", "EquipmentSlotConf.pb", "EquipmentSlotConf",
    new[] { "EquipmentSlotId", "EquipmentSlotColor" });

// ===== Sheet 8: 装备细节类别（EquipmentDetailTypeConf）填充完整数据 =====
// EquipmentPart: 1=主手 2=副手 3=头盔 4=胸甲 5=手套 6=鞋子 7=腰带
// EquipmentTypes: 对应 EquipmentSubCategoryConf.EquipmentSubCategoryId
// EquipmentBaseValueId: 对应 EquipmentBaseValueConf.EquipmentBaseValueId
// 属性需求: Wisdom=敏捷 Strength=力量 Intelligence=智慧
{
    SetupSheet("装备细节类别", "equipment.txt", "EquipmentDetailTypeConf.pb", "EquipmentDetailTypeConf",
        new[] { "EquipmentDetailTypeId", "EquipmentDetailTypeName", "EquipmentDemandLevel",
                "EquipmentHeight", "EquipmentWidth", "EquipmentBaseValueId",
                "EquipmentPart", "EquipmentTypes", "EquipmentDemandWisdom",
                "EquipmentDemandStrength", "EquipmentDemandIntelligence" });
    var ws = pkg.Workbook.Worksheets["装备细节类别"]!;
    // 列说明: Id, 名称, 需求等级, 高, 宽, 基底属性Id, 部位, 小类别Id, 敏捷需求, 力量需求, 智慧需求
    // 武器格子尺寸: 单手3x1, 双手4x2; 防具: 2x2; 腰带/副手: 2x1
    WriteRows(ws, new object[][]
    {
        // ===== 爪 SubCategoryId=1, Part=1(主手) =====
        new object[] {  1, "虎爪",     1, 3, 1,   1, 1, 1,  14,  0,  0 },
        new object[] {  2, "猫爪",     6, 3, 1,   2, 1, 1,  20,  0,  0 },
        new object[] {  3, "鸟爪",    14, 3, 1,   3, 1, 1,  32,  0,  0 },
        new object[] {  4, "双爪",    21, 3, 1,   4, 1, 1,  42,  0,  0 },
        new object[] {  5, "尖爪",    28, 3, 1,   5, 1, 1,  56,  0,  0 },
        new object[] {  6, "战爪",    36, 3, 1,   6, 1, 1,  70,  0,  0 },
        new object[] {  7, "毒爪",    44, 3, 1,   7, 1, 1,  86,  0,  0 },
        new object[] {  8, "幻爪",    52, 3, 1,   8, 1, 1, 100,  0,  0 },
        new object[] {  9, "混沌爪",  60, 3, 1,   9, 1, 1, 113,  0,  0 },
        // ===== 匕首 SubCategoryId=2, Part=1(主手) =====
        new object[] { 10, "粗糙匕首",  1, 3, 1,  10, 1, 2,   0,  0,  0 },
        new object[] { 11, "刺刀",      5, 3, 1,  11, 1, 2,  14,  0,  0 },
        new object[] { 12, "铁匕首",   13, 3, 1,  12, 1, 2,  25,  0,  0 },
        new object[] { 13, "幽灵刃",   21, 3, 1,  13, 1, 2,  40,  0,  0 },
        new object[] { 14, "骨刃",     30, 3, 1,  14, 1, 2,  58,  0,  0 },
        new object[] { 15, "暗影刃",   40, 3, 1,  15, 1, 2,  76,  0,  0 },
        new object[] { 16, "符文匕首", 50, 3, 1,  16, 1, 2,  93,  0,  0 },
        new object[] { 17, "混沌匕首", 60, 3, 1,  17, 1, 2, 113,  0,  0 },
        // ===== 魔杖 SubCategoryId=3, Part=1(主手) =====
        new object[] { 18, "扭曲魔杖",  1, 3, 1,  18, 1, 3,   0,  0,  0 },
        new object[] { 19, "原始魔杖",  5, 3, 1,  19, 1, 3,   0,  0, 14 },
        new object[] { 20, "大魔杖",   13, 3, 1,  20, 1, 3,   0,  0, 25 },
        new object[] { 21, "水晶魔杖", 21, 3, 1,  21, 1, 3,   0,  0, 40 },
        new object[] { 22, "符文魔杖", 30, 3, 1,  22, 1, 3,   0,  0, 58 },
        new object[] { 23, "奥术魔杖", 40, 3, 1,  23, 1, 3,   0,  0, 76 },
        new object[] { 24, "神圣魔杖", 50, 3, 1,  24, 1, 3,   0,  0, 93 },
        new object[] { 25, "混沌魔杖", 60, 3, 1,  25, 1, 3,   0,  0,113 },
        // ===== 单手剑 SubCategoryId=4, Part=1(主手) =====
        new object[] { 26, "铁剑",      1, 3, 1,  26, 1, 4,   0,  0,  0 },
        new object[] { 27, "宽刃剑",    8, 3, 1,  27, 1, 4,   0, 18,  0 },
        new object[] { 28, "战剑",     17, 3, 1,  28, 1, 4,   0, 33,  0 },
        new object[] { 29, "古代剑",   26, 3, 1,  29, 1, 4,   0, 50,  0 },
        new object[] { 30, "精钢剑",   36, 3, 1,  30, 1, 4,   0, 68,  0 },
        new object[] { 31, "骑士剑",   47, 3, 1,  31, 1, 4,   0, 89,  0 },
        new object[] { 32, "混沌剑",   60, 3, 1,  32, 1, 4,   0,113,  0 },
        // ===== 单手斧 SubCategoryId=5, Part=1(主手) =====
        new object[] { 33, "石斧",      1, 3, 1,  33, 1, 5,   0,  0,  0 },
        new object[] { 34, "战斧",      9, 3, 1,  34, 1, 5,   0, 20,  0 },
        new object[] { 35, "裂颅斧",   18, 3, 1,  35, 1, 5,   0, 38,  0 },
        new object[] { 36, "精钢斧",   28, 3, 1,  36, 1, 5,   0, 58,  0 },
        new object[] { 37, "屠夫斧",   40, 3, 1,  37, 1, 5,   0, 80,  0 },
        new object[] { 38, "混沌斧",   55, 3, 1,  38, 1, 5,   0,107,  0 },
        // ===== 单手锤 SubCategoryId=6, Part=1(主手) =====
        new object[] { 39, "木锤",      1, 3, 1,  39, 1, 6,   0,  0,  0 },
        new object[] { 40, "石锤",      8, 3, 1,  40, 1, 6,   0, 18,  0 },
        new object[] { 41, "战锤",     17, 3, 1,  41, 1, 6,   0, 33,  0 },
        new object[] { 42, "精钢锤",   28, 3, 1,  42, 1, 6,   0, 54,  0 },
        new object[] { 43, "破碎锤",   40, 3, 1,  43, 1, 6,   0, 77,  0 },
        new object[] { 44, "混沌锤",   55, 3, 1,  44, 1, 6,   0,107,  0 },
        // ===== 权杖 SubCategoryId=7, Part=1(主手) =====
        new object[] { 45, "木权杖",    1, 3, 1,  45, 1, 7,   0,  0,  0 },
        new object[] { 46, "铁权杖",   10, 3, 1,  46, 1, 7,   0, 20,  0 },
        new object[] { 47, "战权杖",   20, 3, 1,  47, 1, 7,   0, 40,  0 },
        new object[] { 48, "精钢权杖", 33, 3, 1,  48, 1, 7,   0, 65,  0 },
        new object[] { 49, "混沌权杖", 50, 3, 1,  49, 1, 7,   0, 97,  0 },
        // ===== 弓 SubCategoryId=8, Part=1(主手) =====
        new object[] { 50, "木弓",      1, 4, 2,  50, 1, 8,   0,  0,  0 },
        new object[] { 51, "短弓",      8, 4, 2,  51, 1, 8,  18,  0,  0 },
        new object[] { 52, "长弓",     17, 4, 2,  52, 1, 8,  33,  0,  0 },
        new object[] { 53, "复合弓",   26, 4, 2,  53, 1, 8,  50,  0,  0 },
        new object[] { 54, "战弓",     36, 4, 2,  54, 1, 8,  68,  0,  0 },
        new object[] { 55, "精钢弓",   47, 4, 2,  55, 1, 8,  89,  0,  0 },
        new object[] { 56, "混沌弓",   60, 4, 2,  56, 1, 8, 113,  0,  0 },
        // ===== 双手剑 SubCategoryId=9, Part=1(主手) =====
        new object[] { 57, "双手铁剑",  1, 4, 2,  57, 1, 9,   0,  0,  0 },
        new object[] { 58, "双手战剑", 14, 4, 2,  58, 1, 9,   0, 28,  0 },
        new object[] { 59, "双手古代剑",26,4, 2,  59, 1, 9,   0, 50,  0 },
        new object[] { 60, "双手精钢剑",40,4, 2,  60, 1, 9,   0, 77,  0 },
        new object[] { 61, "双手混沌剑",55,4, 2,  61, 1, 9,   0,107,  0 },
        // ===== 双手斧 SubCategoryId=10, Part=1(主手) =====
        new object[] { 62, "双手石斧",  1, 4, 2,  62, 1,10,   0,  0,  0 },
        new object[] { 63, "双手战斧", 14, 4, 2,  63, 1,10,   0, 28,  0 },
        new object[] { 64, "双手裂颅斧",26,4, 2,  64, 1,10,   0, 50,  0 },
        new object[] { 65, "双手精钢斧",40,4, 2,  65, 1,10,   0, 77,  0 },
        new object[] { 66, "双手混沌斧",55,4, 2,  66, 1,10,   0,107,  0 },
        // ===== 双手锤 SubCategoryId=11, Part=1(主手) =====
        new object[] { 67, "双手木锤",  1, 4, 2,  67, 1,11,   0,  0,  0 },
        new object[] { 68, "双手石锤", 14, 4, 2,  68, 1,11,   0, 28,  0 },
        new object[] { 69, "双手战锤", 26, 4, 2,  69, 1,11,   0, 50,  0 },
        new object[] { 70, "双手精钢锤",40,4, 2,  70, 1,11,   0, 77,  0 },
        new object[] { 71, "双手混沌锤",55,4, 2,  71, 1,11,   0,107,  0 },
        // ===== 长矛 SubCategoryId=12, Part=1(主手) =====
        new object[] { 72, "木矛",      1, 4, 2,  72, 1,12,   0,  0,  0 },
        new object[] { 73, "铁矛",     13, 4, 2,  73, 1,12,   0, 26,  0 },
        new object[] { 74, "战矛",     26, 4, 2,  74, 1,12,   0, 50,  0 },
        new object[] { 75, "精钢矛",   40, 4, 2,  75, 1,12,   0, 77,  0 },
        new object[] { 76, "混沌矛",   55, 4, 2,  76, 1,12,   0,107,  0 },
        // ===== 法杖 SubCategoryId=13, Part=1(主手) =====
        new object[] { 77, "木法杖",    1, 4, 2,  77, 1,13,   0,  0,  0 },
        new object[] { 78, "铁法杖",   13, 4, 2,  78, 1,13,   0,  0, 26 },
        new object[] { 79, "战法杖",   26, 4, 2,  79, 1,13,   0,  0, 50 },
        new object[] { 80, "精钢法杖", 40, 4, 2,  80, 1,13,   0,  0, 77 },
        new object[] { 81, "混沌法杖", 55, 4, 2,  81, 1,13,   0,  0,107 },
        // ===== 头盔 SubCategoryId=14, Part=3(头盔) =====
        new object[] { 82, "铁帽",      1, 2, 2,  82, 3,14,   0,  0,  0 },
        new object[] { 83, "战盔",     14, 2, 2,  83, 3,14,   0, 28,  0 },
        new object[] { 84, "大盔",     30, 2, 2,  84, 3,14,   0, 58,  0 },
        new object[] { 85, "精钢盔",   50, 2, 2,  85, 3,14,   0, 97,  0 },
        new object[] { 86, "皮帽",      1, 2, 2,  86, 3,14,   0,  0,  0 },
        new object[] { 87, "皮盔",     14, 2, 2,  87, 3,14,  28,  0,  0 },
        new object[] { 88, "猎人帽",   30, 2, 2,  88, 3,14,  58,  0,  0 },
        new object[] { 89, "精钢皮盔", 50, 2, 2,  89, 3,14,  97,  0,  0 },
        new object[] { 90, "布帽",      1, 2, 2,  90, 3,14,   0,  0,  0 },
        new object[] { 91, "法师帽",   14, 2, 2,  91, 3,14,   0,  0, 28 },
        new object[] { 92, "大法师帽", 30, 2, 2,  92, 3,14,   0,  0, 58 },
        new object[] { 93, "精钢法师帽",50,2, 2,  93, 3,14,   0,  0, 97 },
        // ===== 胸甲 SubCategoryId=15, Part=4(胸甲) =====
        new object[] { 94, "铁甲",      1, 3, 2,  94, 4,15,   0,  0,  0 },
        new object[] { 95, "战甲",     14, 3, 2,  95, 4,15,   0, 28,  0 },
        new object[] { 96, "板甲",     30, 3, 2,  96, 4,15,   0, 58,  0 },
        new object[] { 97, "精钢甲",   50, 3, 2,  97, 4,15,   0, 97,  0 },
        new object[] { 98, "皮甲",      1, 3, 2,  98, 4,15,   0,  0,  0 },
        new object[] { 99, "猎人甲",   14, 3, 2,  99, 4,15,  28,  0,  0 },
        new object[] {100, "精钢皮甲", 30, 3, 2, 100, 4,15,  58,  0,  0 },
        new object[] {101, "布甲",      1, 3, 2, 101, 4,15,   0,  0,  0 },
        new object[] {102, "法师甲",   14, 3, 2, 102, 4,15,   0,  0, 28 },
        new object[] {103, "精钢法师甲",30,3, 2, 103, 4,15,   0,  0, 58 },
        // ===== 手套 SubCategoryId=16, Part=5(手套) =====
        new object[] {104, "铁手套",    1, 2, 2, 104, 5,16,   0,  0,  0 },
        new object[] {105, "战手套",   20, 2, 2, 105, 5,16,   0, 40,  0 },
        new object[] {106, "精钢手套", 40, 2, 2, 106, 5,16,   0, 77,  0 },
        new object[] {107, "皮手套",    1, 2, 2, 107, 5,16,   0,  0,  0 },
        new object[] {108, "猎人手套", 20, 2, 2, 108, 5,16,  40,  0,  0 },
        new object[] {109, "精钢皮手套",40,2, 2, 109, 5,16,  77,  0,  0 },
        new object[] {110, "布手套",    1, 2, 2, 110, 5,16,   0,  0,  0 },
        new object[] {111, "法师手套", 20, 2, 2, 111, 5,16,   0,  0, 40 },
        new object[] {112, "精钢法师手套",40,2,2,112, 5,16,   0,  0, 77 },
        // ===== 鞋子 SubCategoryId=17, Part=6(鞋子) =====
        new object[] {113, "铁靴",      1, 2, 2, 113, 6,17,   0,  0,  0 },
        new object[] {114, "战靴",     20, 2, 2, 114, 6,17,   0, 40,  0 },
        new object[] {115, "精钢靴",   40, 2, 2, 115, 6,17,   0, 77,  0 },
        new object[] {116, "皮靴",      1, 2, 2, 116, 6,17,   0,  0,  0 },
        new object[] {117, "猎人靴",   20, 2, 2, 117, 6,17,  40,  0,  0 },
        new object[] {118, "精钢皮靴", 40, 2, 2, 118, 6,17,  77,  0,  0 },
        new object[] {119, "布靴",      1, 2, 2, 119, 6,17,   0,  0,  0 },
        new object[] {120, "法师靴",   20, 2, 2, 120, 6,17,   0,  0, 40 },
        new object[] {121, "精钢法师靴",40,2, 2, 121, 6,17,   0,  0, 77 },
        // ===== 腰带 SubCategoryId=18, Part=7(饰品) =====
        new object[] {122, "皮腰带",    1, 1, 2, 122, 7,18,   0,  0,  0 },
        new object[] {123, "重型腰带",  1, 1, 2, 123, 7,18,   0,  0,  0 },
        new object[] {124, "腰带",      1, 1, 2, 124, 7,18,   0,  0,  0 },
        new object[] {125, "绳腰带",    1, 1, 2, 125, 7,18,   0,  0,  0 },
        // ===== 戒指 SubCategoryId=19, Part=7(饰品) =====
        new object[] {134, "铁戒指",     1, 1, 1, 134, 7,19,   0,  0,  0 },
        new object[] {135, "金戒指",     1, 1, 1, 135, 7,19,   0,  0,  0 },
        new object[] {136, "宝石戒指",   1, 1, 1, 136, 7,19,   0,  0,  0 },
        // ===== 项链 SubCategoryId=20, Part=7(饰品) =====
        new object[] {137, "铁项链",     1, 1, 1, 137, 7,20,   0,  0,  0 },
        new object[] {138, "金项链",     1, 1, 1, 138, 7,20,   0,  0,  0 },
        new object[] {139, "宝石项链",   1, 1, 1, 139, 7,20,   0,  0,  0 },
        // ===== 盾牌 SubCategoryId=21, Part=2(副手) =====
        new object[] {126, "木盾",      1, 3, 2, 126, 2,21,   0,  0,  0 },
        new object[] {127, "铁盾",     14, 3, 2, 127, 2,21,   0, 28,  0 },
        new object[] {128, "战盾",     30, 3, 2, 128, 2,21,   0, 58,  0 },
        new object[] {129, "精钓盾",   50, 3, 2, 129, 2,21,   0, 97,  0 },
        // ===== 箭袋 SubCategoryId=22, Part=2(副手) =====
        new object[] {130, "箭袋",      1, 3, 2, 130, 2,22,   0,  0,  0 },
        new object[] {131, "精钓箭袋", 30, 3, 2, 131, 2,22,   0,  0,  0 },
        // ===== 法器 SubCategoryId=23, Part=2(副手) =====
        new object[] {132, "法球",      1, 2, 2, 132, 2,23,   0,  0,  0 },
        new object[] {133, "精钓法球", 30, 2, 2, 133, 2,23,   0,  0, 58 },
    });
}

// ===== Sheet 9: 装备部位 =====
{
    SetupSheet("装备部位", "equipment.txt", "EquipmentPartConf.pb", "EquipmentPartConf",
        new[] { "EquipmentPartId", "EquipmentPartName", "CanDualWield" });
    var ws = pkg.Workbook.Worksheets["装备部位"]!;
    WriteRows(ws, new object[][]
    {
        new object[] { 1, "主手", true  },
        new object[] { 2, "副手", true  },
        new object[] { 3, "头盔", false },
        new object[] { 4, "胸甲", false },
        new object[] { 5, "手套", false },
        new object[] { 6, "鞋子", false },
        new object[] { 7, "饰品", false },
    });
}

// ===== Sheet 10: 装备大类别 =====
{
    SetupSheet("装备大类别", "equipment.txt", "EquipmentCategoryConf.pb", "EquipmentCategoryConf",
        new[] { "EquipmentCategoryId", "EquipmentCategoryName", "EquipmentPart", "EquipmentMaxSlot" });
    var ws = pkg.Workbook.Worksheets["装备大类别"]!;
    // EquipmentMaxSlot: 单手武器=3, 双手武器/胸甲=6, 头盔/手套/鞋子/副手=4, 饰品按小类别控制（腰带=2、戒指=1、项链=0）
    WriteRows(ws, new object[][]
    {
        new object[] { 1, "单手武器", 1, 3 },
        new object[] { 2, "双手武器", 1, 6 },
        new object[] { 3, "头盔",     3, 4 },
        new object[] { 4, "胸甲",     4, 6 },
        new object[] { 5, "手套",     5, 4 },
        new object[] { 6, "鞋子",     6, 4 },
        new object[] { 7, "饰品",     7, 0 },
        new object[] { 8, "副手",     2, 4 },
    });
}

// ===== Sheet 11: 装备小类别 =====
{
    SetupSheet("装备小类别", "equipment.txt", "EquipmentSubCategoryConf.pb", "EquipmentSubCategoryConf",
        new[] { "EquipmentSubCategoryId", "EquipmentSubCategoryName", "EquipmentCategoryId", "EquipmentMaxSlot" });
    var ws = pkg.Workbook.Worksheets["装备小类别"]!;
    WriteRows(ws, new object[][]
    {
        new object[] {  1, "爪",     1, 3 },
        new object[] {  2, "匕首",   1, 3 },
        new object[] {  3, "魔杖",   1, 3 },
        new object[] {  4, "单手剑", 1, 3 },
        new object[] {  5, "单手斧", 1, 3 },
        new object[] {  6, "单手锤", 1, 3 },
        new object[] {  7, "权杖",   1, 3 },
        new object[] {  8, "弓",     2, 6 },
        new object[] {  9, "双手剑", 2, 6 },
        new object[] { 10, "双手斧", 2, 6 },
        new object[] { 11, "双手锤", 2, 6 },
        new object[] { 12, "长矛",   2, 6 },
        new object[] { 13, "法杖",   2, 6 },
        new object[] { 14, "头盔",   3, 4 },
        new object[] { 15, "胸甲",   4, 6 },
        new object[] { 16, "手套",   5, 4 },
        new object[] { 17, "鞋子",   6, 4 },
        new object[] { 18, "腰带",   7, 2 },
        new object[] { 19, "戒指",   7, 1 },
        new object[] { 20, "项链",   7, 0 },
        new object[] { 21, "盾牌",   8, 4 },
        new object[] { 22, "箭袋",   8, 4 },
        new object[] { 23, "法器",   8, 4 },
    });
}

// ===== Sheet 12: 词缀定义（EquipmentModConf）=====
// EquipmentModType: 1=前缀 2=后缀
// EquipmentModTier: 词缀等阶（1最高）
// EquipmentModSubCategories: 适用的小类别ID（1=爪，可扩展多个）
// 爪专属词缀数据来源：https://poedb.tw/cn/Claws#ModifiersCalc
{
    SetupSheet("词缀定义", "equipment.txt", "EquipmentModConf.pb", "EquipmentModConf",
        new[] { "EquipmentModId", "EquipmentModName", "EquipmentModType",
                "EquipmentModTier", "EquipmentModRequireLevel", "EquipmentModSubCategories", "EquipmentModWeight" });
    var ws = pkg.Workbook.Worksheets["词缀定义"]!;
    // 列说明: Id, 词缀名, 类型(1前缀/2后缀), 等阶, 需求等级, 适用小类别Id列表(|分隔), 权重(越高越容易出现)
    // ===== 前缀 =====
    // 物理伤害前缀（爪专属）
    // 攻击速度前缀（爪专属）
    // 暴击率前缀（爪/匕首共用）
    // 生命偷取前缀（爪专属）
    // 魔力偷取前缀（爪专属）
    WriteRows(ws, new object[][]
    {
        // ===== 前缀（EquipmentModType=1）=====
        // 物理伤害 - 爪专属前缀（6阶）权重：T1最低T6最高
        new object[] {  1, "残忍",         1, 1, 60, "1",  100 }, // 增加 (35-45) 到 (70-80) 物理伤害
        new object[] {  2, "凶猛",         1, 2, 46, "1",  200 }, // 增加 (25-32) 到 (50-62) 物理伤害
        new object[] {  3, "锋利",         1, 3, 35, "1",  400 }, // 增加 (18-22) 到 (36-44) 物理伤害
        new object[] {  4, "尖锐",         1, 4, 23, "1",  700 }, // 增加 (12-15) 到 (24-30) 物理伤害
        new object[] {  5, "锐利",         1, 5, 11, "1", 1000 }, // 增加 (7-9) 到 (14-18) 物理伤害
        new object[] {  6, "磨砺",         1, 6,  1, "1", 1500 }, // 增加 (2-4) 到 (5-8) 物理伤害
        // 攻击速度 - 爪专属前缀（5阶）
        new object[] {  7, "迅捷",         1, 1, 60, "1",  100 }, // 攻击速度提高 (20-24)%
        new object[] {  8, "快速",         1, 2, 46, "1",  200 }, // 攻击速度提高 (15-19)%
        new object[] {  9, "敏捷",         1, 3, 30, "1",  400 }, // 攻击速度提高 (11-14)%
        new object[] { 10, "灵活",         1, 4, 15, "1",  700 }, // 攻击速度提高 (7-10)%
        new object[] { 11, "轻盈",         1, 5,  1, "1", 1000 }, // 攻击速度提高 (3-6)%
        // 暴击率 - 爪/匕首共用前缀（5阶）
        new object[] { 12, "精准",         1, 1, 60, "1|2",  100 }, // 暴击率提高 (35-40)%
        new object[] { 13, "犀利",         1, 2, 44, "1|2",  200 }, // 暴击率提高 (25-29)%
        new object[] { 14, "致命",         1, 3, 30, "1|2",  400 }, // 暴击率提高 (18-22)%
        new object[] { 15, "锐利",         1, 4, 15, "1|2",  700 }, // 暴击率提高 (10-14)%
        new object[] { 16, "尖刺",         1, 5,  1, "1|2", 1000 }, // 暴击率提高 (5-9)%
        // 生命偷取 - 爪专属前缀（4阶）
        new object[] { 17, "吸血",         1, 1, 50, "1",  100 }, // 造成物理伤害时偷取 (2.0-2.4)% 生命
        new object[] { 18, "汲取",         1, 2, 35, "1",  250 }, // 造成物理伤害时偷取 (1.5-1.9)% 生命
        new object[] { 19, "汲血",         1, 3, 20, "1",  500 }, // 造成物理伤害时偷取 (1.0-1.4)% 生命
        new object[] { 20, "渗血",         1, 4,  1, "1", 1000 }, // 造成物理伤害时偷取 (0.4-0.9)% 生命
        // 魔力偷取 - 爪专属前缀（4阶）
        new object[] { 21, "汲魔",         1, 1, 50, "1",  100 }, // 造成物理伤害时偷取 (2.0-2.4)% 魔力
        new object[] { 22, "汲取魔力",     1, 2, 35, "1",  250 }, // 造成物理伤害时偷取 (1.5-1.9)% 魔力
        new object[] { 23, "魔力汲取",     1, 3, 20, "1",  500 }, // 造成物理伤害时偷取 (1.0-1.4)% 魔力
        new object[] { 24, "魔力渗取",     1, 4,  1, "1", 1000 }, // 造成物理伤害时偷取 (0.4-0.9)% 魔力
        // 暴击伤害 - 爪/匕首共用前缀（5阶）
        new object[] { 25, "毁灭",         1, 1, 60, "1|2",  100 }, // 暴击伤害加成 (50-59)%
        new object[] { 26, "破坏",         1, 2, 44, "1|2",  200 }, // 暴击伤害加成 (35-44)%
        new object[] { 27, "重击",         1, 3, 30, "1|2",  400 }, // 暴击伤害加成 (25-29)%
        new object[] { 28, "强力",         1, 4, 15, "1|2",  700 }, // 暴击伤害加成 (15-19)%
        new object[] { 29, "有力",         1, 5,  1, "1|2", 1000 }, // 暴击伤害加成 (5-9)%

        // ===== 后缀（EquipmentModType=2）=====
        // 攻击速度 - 爪后缀（4阶）
        new object[] { 30, "的迅速",       2, 1, 55, "1",  100 }, // 攻击速度提高 (13-15)%
        new object[] { 31, "的速度",       2, 2, 38, "1",  250 }, // 攻击速度提高 (9-12)%
        new object[] { 32, "的敏捷",       2, 3, 20, "1",  500 }, // 攻击速度提高 (5-8)%
        new object[] { 33, "的轻盈",       2, 4,  1, "1", 1000 }, // 攻击速度提高 (2-4)%
        // 暴击率 - 爪后缀（4阶）
        new object[] { 34, "的精准",       2, 1, 55, "1",  100 }, // 暴击率提高 (25-30)%
        new object[] { 35, "的犀利",       2, 2, 38, "1",  250 }, // 暴击率提高 (15-19)%
        new object[] { 36, "的致命",       2, 3, 20, "1",  500 }, // 暴击率提高 (8-12)%
        new object[] { 37, "的锐利",       2, 4,  1, "1", 1000 }, // 暴击率提高 (3-7)%
        // 命中 - 爪后缀（4阶）
        new object[] { 38, "的命中",       2, 1, 55, "1",  100 }, // 命中值 +(300-350)
        new object[] { 39, "的准确",       2, 2, 38, "1",  250 }, // 命中值 +(175-225)
        new object[] { 40, "的瞄准",       2, 3, 20, "1",  500 }, // 命中值 +(100-150)
        new object[] { 41, "的精确",       2, 4,  1, "1", 1000 }, // 命中值 +(30-60)
        // 力量 - 通用后缀（4阶）
        new object[] { 42, "的力量",       2, 1, 55, "1",  100 }, // 力量 +(23-27)
        new object[] { 43, "的强壮",       2, 2, 38, "1",  250 }, // 力量 +(16-22)
        new object[] { 44, "的体魄",       2, 3, 20, "1",  500 }, // 力量 +(9-15)
        new object[] { 45, "的肌肉",       2, 4,  1, "1", 1000 }, // 力量 +(3-8)
        // 敏捷 - 通用后缀（4阶）
        new object[] { 46, "的敏捷度",     2, 1, 55, "1",  100 }, // 敏捷 +(23-27)
        new object[] { 47, "的灵巧",       2, 2, 38, "1",  250 }, // 敏捷 +(16-22)
        new object[] { 48, "的身法",       2, 3, 20, "1",  500 }, // 敏捷 +(9-15)
        new object[] { 49, "的轻功",       2, 4,  1, "1", 1000 }, // 敏捷 +(3-8)
        // 智慧 - 通用后缀（4阶）
        new object[] { 50, "的智慧",       2, 1, 55, "1",  100 }, // 智慧 +(23-27)
        new object[] { 51, "的睿智",       2, 2, 38, "1",  250 }, // 智慧 +(16-22)
        new object[] { 52, "的学识",       2, 3, 20, "1",  500 }, // 智慧 +(9-15)
        new object[] { 53, "的聪慧",       2, 4,  1, "1", 1000 }, // 智慧 +(3-8)
        // 火焰抗性 - 通用后缀（4阶）
        new object[] { 54, "的火焰",       2, 1, 55, "1",  100 }, // 火焰抗性 +(24-28)%
        new object[] { 55, "的炎热",       2, 2, 38, "1",  250 }, // 火焰抗性 +(16-23)%
        new object[] { 56, "的温暖",       2, 3, 20, "1",  500 }, // 火焰抗性 +(9-15)%
        new object[] { 57, "的灼热",       2, 4,  1, "1", 1000 }, // 火焰抗性 +(3-8)%
        // 冰霜抗性 - 通用后缀（4阶）
        new object[] { 58, "的冰霜",       2, 1, 55, "1",  100 }, // 冰霜抗性 +(24-28)%
        new object[] { 59, "的寒冷",       2, 2, 38, "1",  250 }, // 冰霜抗性 +(16-23)%
        new object[] { 60, "的冰冻",       2, 3, 20, "1",  500 }, // 冰霜抗性 +(9-15)%
        new object[] { 61, "的寒意",       2, 4,  1, "1", 1000 }, // 冰霜抗性 +(3-8)%
        // 闪电抗性 - 通用后缀（4阶）
        new object[] { 62, "的闪电",       2, 1, 55, "1",  100 }, // 闪电抗性 +(24-28)%
        new object[] { 63, "的雷霆",       2, 2, 38, "1",  250 }, // 闪电抗性 +(16-23)%
        new object[] { 64, "的电流",       2, 3, 20, "1",  500 }, // 闪电抗性 +(9-15)%
        new object[] { 65, "的静电",       2, 4,  1, "1", 1000 }, // 闪电抗性 +(3-8)%
        // 生命 - 通用后缀（5阶）
        new object[] { 66, "的活力",       2, 1, 60, "1",  100 }, // 最大生命 +(60-79)
        new object[] { 67, "的生命",       2, 2, 46, "1",  200 }, // 最大生命 +(45-59)
        new object[] { 68, "的健康",       2, 3, 30, "1",  400 }, // 最大生命 +(30-44)
        new object[] { 69, "的体力",       2, 4, 15, "1",  700 }, // 最大生命 +(15-29)
        new object[] { 70, "的耐力",       2, 5,  1, "1", 1000 }, // 最大生命 +(3-14)
    });
}

// ===== Sheet 13: 词缀数值范围（EquipmentModValueConf）=====
// 每条词缀可能有1~2个数值范围（如"增加X到Y物理伤害"有两个值）
// EquipmentModValueIndex: 同一词缀的第几个数值（1或2）
{
    SetupSheet("词缀数值范围", "equipment.txt", "EquipmentModValueConf.pb", "EquipmentModValueConf",
        new[] { "EquipmentModValueId", "EquipmentModId", "EquipmentModValueIndex",
                "EquipmentModMinValue", "EquipmentModMaxValue", "EquipmentModValueDesc" });
    var ws = pkg.Workbook.Worksheets["词缀数值范围"]!;
    WriteRows(ws, new object[][]
    {
        // ===== 物理伤害前缀（ModId 1-6，每条2个数值：最小伤害范围 + 最大伤害范围）=====
        // ModId=1 残忍
        new object[] {  1,  1, 1,  35,  45, "增加最小物理伤害" },
        new object[] {  2,  1, 2,  70,  80, "增加最大物理伤害" },
        // ModId=2 凶猛
        new object[] {  3,  2, 1,  25,  32, "增加最小物理伤害" },
        new object[] {  4,  2, 2,  50,  62, "增加最大物理伤害" },
        // ModId=3 锋利
        new object[] {  5,  3, 1,  18,  22, "增加最小物理伤害" },
        new object[] {  6,  3, 2,  36,  44, "增加最大物理伤害" },
        // ModId=4 尖锐
        new object[] {  7,  4, 1,  12,  15, "增加最小物理伤害" },
        new object[] {  8,  4, 2,  24,  30, "增加最大物理伤害" },
        // ModId=5 锐利
        new object[] {  9,  5, 1,   7,   9, "增加最小物理伤害" },
        new object[] { 10,  5, 2,  14,  18, "增加最大物理伤害" },
        // ModId=6 磨砺
        new object[] { 11,  6, 1,   2,   4, "增加最小物理伤害" },
        new object[] { 12,  6, 2,   5,   8, "增加最大物理伤害" },

        // ===== 攻击速度前缀（ModId 7-11，单数值：攻击速度%）=====
        new object[] { 13,  7, 1,  20,  24, "攻击速度提高%" },
        new object[] { 14,  8, 1,  15,  19, "攻击速度提高%" },
        new object[] { 15,  9, 1,  11,  14, "攻击速度提高%" },
        new object[] { 16, 10, 1,   7,  10, "攻击速度提高%" },
        new object[] { 17, 11, 1,   3,   6, "攻击速度提高%" },

        // ===== 暴击率前缀（ModId 12-16，单数值：暴击率%）=====
        new object[] { 18, 12, 1,  35,  40, "暴击率提高%" },
        new object[] { 19, 13, 1,  25,  29, "暴击率提高%" },
        new object[] { 20, 14, 1,  18,  22, "暴击率提高%" },
        new object[] { 21, 15, 1,  10,  14, "暴击率提高%" },
        new object[] { 22, 16, 1,   5,   9, "暴击率提高%" },

        // ===== 生命偷取前缀（ModId 17-20，数值×10存储，实际/10使用）=====
        new object[] { 23, 17, 1,  20,  24, "物理伤害生命偷取‰" }, // 2.0-2.4%
        new object[] { 24, 18, 1,  15,  19, "物理伤害生命偷取‰" }, // 1.5-1.9%
        new object[] { 25, 19, 1,  10,  14, "物理伤害生命偷取‰" }, // 1.0-1.4%
        new object[] { 26, 20, 1,   4,   9, "物理伤害生命偷取‰" }, // 0.4-0.9%

        // ===== 魔力偷取前缀（ModId 21-24）=====
        new object[] { 27, 21, 1,  20,  24, "物理伤害魔力偷取‰" },
        new object[] { 28, 22, 1,  15,  19, "物理伤害魔力偷取‰" },
        new object[] { 29, 23, 1,  10,  14, "物理伤害魔力偷取‰" },
        new object[] { 30, 24, 1,   4,   9, "物理伤害魔力偷取‰" },

        // ===== 暴击伤害前缀（ModId 25-29）=====
        new object[] { 31, 25, 1,  50,  59, "暴击伤害加成%" },
        new object[] { 32, 26, 1,  35,  44, "暴击伤害加成%" },
        new object[] { 33, 27, 1,  25,  29, "暴击伤害加成%" },
        new object[] { 34, 28, 1,  15,  19, "暴击伤害加成%" },
        new object[] { 35, 29, 1,   5,   9, "暴击伤害加成%" },

        // ===== 攻击速度后缀（ModId 30-33）=====
        new object[] { 36, 30, 1,  13,  15, "攻击速度提高%" },
        new object[] { 37, 31, 1,   9,  12, "攻击速度提高%" },
        new object[] { 38, 32, 1,   5,   8, "攻击速度提高%" },
        new object[] { 39, 33, 1,   2,   4, "攻击速度提高%" },

        // ===== 暴击率后缀（ModId 34-37）=====
        new object[] { 40, 34, 1,  25,  30, "暴击率提高%" },
        new object[] { 41, 35, 1,  15,  19, "暴击率提高%" },
        new object[] { 42, 36, 1,   8,  12, "暴击率提高%" },
        new object[] { 43, 37, 1,   3,   7, "暴击率提高%" },

        // ===== 命中后缀（ModId 38-41）=====
        new object[] { 44, 38, 1, 300, 350, "命中值+" },
        new object[] { 45, 39, 1, 175, 225, "命中值+" },
        new object[] { 46, 40, 1, 100, 150, "命中值+" },
        new object[] { 47, 41, 1,  30,  60, "命中值+" },

        // ===== 力量后缀（ModId 42-45）=====
        new object[] { 48, 42, 1,  23,  27, "力量+" },
        new object[] { 49, 43, 1,  16,  22, "力量+" },
        new object[] { 50, 44, 1,   9,  15, "力量+" },
        new object[] { 51, 45, 1,   3,   8, "力量+" },

        // ===== 敏捷后缀（ModId 46-49）=====
        new object[] { 52, 46, 1,  23,  27, "敏捷+" },
        new object[] { 53, 47, 1,  16,  22, "敏捷+" },
        new object[] { 54, 48, 1,   9,  15, "敏捷+" },
        new object[] { 55, 49, 1,   3,   8, "敏捷+" },

        // ===== 智慧后缀（ModId 50-53）=====
        new object[] { 56, 50, 1,  23,  27, "智慧+" },
        new object[] { 57, 51, 1,  16,  22, "智慧+" },
        new object[] { 58, 52, 1,   9,  15, "智慧+" },
        new object[] { 59, 53, 1,   3,   8, "智慧+" },

        // ===== 火焰抗性后缀（ModId 54-57）=====
        new object[] { 60, 54, 1,  24,  28, "火焰抗性+%" },
        new object[] { 61, 55, 1,  16,  23, "火焰抗性+%" },
        new object[] { 62, 56, 1,   9,  15, "火焰抗性+%" },
        new object[] { 63, 57, 1,   3,   8, "火焰抗性+%" },

        // ===== 冰霜抗性后缀（ModId 58-61）=====
        new object[] { 64, 58, 1,  24,  28, "冰霜抗性+%" },
        new object[] { 65, 59, 1,  16,  23, "冰霜抗性+%" },
        new object[] { 66, 60, 1,   9,  15, "冰霜抗性+%" },
        new object[] { 67, 61, 1,   3,   8, "冰霜抗性+%" },

        // ===== 闪电抗性后缀（ModId 62-65）=====
        new object[] { 68, 62, 1,  24,  28, "闪电抗性+%" },
        new object[] { 69, 63, 1,  16,  23, "闪电抗性+%" },
        new object[] { 70, 64, 1,   9,  15, "闪电抗性+%" },
        new object[] { 71, 65, 1,   3,   8, "闪电抗性+%" },

        // ===== 生命后缀（ModId 66-70）=====
        new object[] { 72, 66, 1,  60,  79, "最大生命+" },
        new object[] { 73, 67, 1,  45,  59, "最大生命+" },
        new object[] { 74, 68, 1,  30,  44, "最大生命+" },
        new object[] { 75, 69, 1,  15,  29, "最大生命+" },
        new object[] { 76, 70, 1,   3,  14, "最大生命+" },
    });
}

// ===== Sheet 14: 药剂基底（FlaskBaseConf）=====
// FlaskType: 0=Life 1=Mana 2=Hybrid 3=Utility
// FlaskUtilityEffectType: 0=None 1=MoveSpeed 2=Armour 3=Evasion 4=FireResistance 5=ColdResistance
// 6=LightningResistance 7=ChaosResistance 8=PhysicalDamageReduction 9=ConsecratedGround 10=Phasing 11=Onslaught
// FlaskAllowedSlots: 药剂可放入的槽位，使用逗号分隔，对应 EquipmentSlot.Flask1~Flask5 => 10,11,12,13,14
{
    SetupSheet("药剂基底", "equipment.txt", "FlaskBaseConf.pb", "FlaskBaseConf",
        new[]
        {
            "FlaskBaseId", "FlaskCode", "FlaskName", "FlaskType", "FlaskRequireLevel",
            "FlaskWidth", "FlaskHeight", "FlaskRecoverLife", "FlaskRecoverMana", "FlaskDurationMs",
            "FlaskMaxCharges", "FlaskChargesPerUse", "FlaskIsInstant", "FlaskInstantPercent",
            "FlaskUtilityEffectType", "FlaskUtilityEffectValue", "FlaskAllowedSlots", "FlaskEffectDesc"
        });
    var ws = pkg.Workbook.Worksheets["药剂基底"]!;
    const string allFlaskSlots = "10,11,12,13,14";
    WriteRows(ws, new object[][]
    {
        new object[] {  1, "life_small",          "小型生命药剂",       0,  1, 1, 2,  70,   0, 4000, 21,  7, false,   0, 0,  0, allFlaskSlots, "4.00 秒内恢复 70 点生命" },
        new object[] {  2, "life_medium",         "中型生命药剂",       0,  6, 1, 2, 150,   0, 4500, 24,  8, false,   0, 0,  0, allFlaskSlots, "4.50 秒内恢复 150 点生命" },
        new object[] {  3, "life_large",          "大型生命药剂",       0, 12, 1, 2, 260,   0, 5000, 28, 10, false,   0, 0,  0, allFlaskSlots, "5.00 秒内恢复 260 点生命" },
        new object[] {  4, "life_greater",        "巨型生命药剂",       0, 18, 1, 2, 430,   0, 5500, 32, 11, false,   0, 0,  0, allFlaskSlots, "5.50 秒内恢复 430 点生命" },
        new object[] {  5, "life_grand",          "宏伟生命药剂",       0, 24, 1, 2, 620,   0, 6000, 36, 12, false,   0, 0,  0, allFlaskSlots, "6.00 秒内恢复 620 点生命" },
        new object[] {  6, "life_divine",         "圣神生命药剂",       0, 36, 1, 2, 980,   0, 7000, 45, 15, false,   0, 0,  0, allFlaskSlots, "7.00 秒内恢复 980 点生命" },
        new object[] {  7, "life_hallowed",       "祝福生命药剂",       0, 50, 1, 2, 1460,  0, 7000, 50, 15, false,   0, 0,  0, allFlaskSlots, "7.00 秒内恢复 1460 点生命" },
        new object[] {  8, "life_panicked",       "惊惧生命药剂",       0, 20, 1, 2, 520,   0, 2500, 32, 10, true,   70, 0,  0, allFlaskSlots, "瞬间恢复 70% 生命，剩余部分在 2.50 秒内恢复" },

        new object[] {  9, "mana_small",          "小型魔力药剂",       1,  1, 1, 2,   0,  50, 5000, 28,  6, false,   0, 0,  0, allFlaskSlots, "5.00 秒内恢复 50 点魔力" },
        new object[] { 10, "mana_medium",         "中型魔力药剂",       1,  6, 1, 2,   0, 110, 5000, 32,  7, false,   0, 0,  0, allFlaskSlots, "5.00 秒内恢复 110 点魔力" },
        new object[] { 11, "mana_large",          "大型魔力药剂",       1, 12, 1, 2,   0, 180, 5000, 36,  8, false,   0, 0,  0, allFlaskSlots, "5.00 秒内恢复 180 点魔力" },
        new object[] { 12, "mana_greater",        "巨型魔力药剂",       1, 18, 1, 2,   0, 290, 5500, 40,  9, false,   0, 0,  0, allFlaskSlots, "5.50 秒内恢复 290 点魔力" },
        new object[] { 13, "mana_grand",          "宏伟魔力药剂",       1, 24, 1, 2,   0, 430, 6000, 44, 10, false,   0, 0,  0, allFlaskSlots, "6.00 秒内恢复 430 点魔力" },
        new object[] { 14, "mana_divine",         "圣神魔力药剂",       1, 36, 1, 2,   0, 680, 7000, 50, 12, false,   0, 0,  0, allFlaskSlots, "7.00 秒内恢复 680 点魔力" },
        new object[] { 15, "mana_eternal",        "永恒魔力药剂",       1, 50, 1, 2,   0, 920, 7000, 55, 12, false,   0, 0,  0, allFlaskSlots, "7.00 秒内恢复 920 点魔力" },

        new object[] { 16, "hybrid_small",        "小型复合药剂",       2, 10, 1, 2,  80,  50, 5000, 30, 10, false,   0, 0,  0, allFlaskSlots, "5.00 秒内同时恢复 80 点生命与 50 点魔力" },
        new object[] { 17, "hybrid_medium",       "中型复合药剂",       2, 20, 1, 2, 160, 100, 5000, 36, 11, false,   0, 0,  0, allFlaskSlots, "5.00 秒内同时恢复 160 点生命与 100 点魔力" },
        new object[] { 18, "hybrid_large",        "大型复合药剂",       2, 30, 1, 2, 300, 180, 5500, 40, 12, false,   0, 0,  0, allFlaskSlots, "5.50 秒内同时恢复 300 点生命与 180 点魔力" },
        new object[] { 19, "hybrid_greater",      "巨型复合药剂",       2, 42, 1, 2, 470, 280, 6000, 46, 13, false,   0, 0,  0, allFlaskSlots, "6.00 秒内同时恢复 470 点生命与 280 点魔力" },
        new object[] { 20, "hybrid_divine",       "圣神复合药剂",       2, 54, 1, 2, 720, 430, 6500, 52, 14, false,   0, 0,  0, allFlaskSlots, "6.50 秒内同时恢复 720 点生命与 430 点魔力" },

        new object[] { 21, "utility_quicksilver", "水银药剂",           3,  4, 1, 2,   0,   0, 5000, 60, 30, false,   0, 1, 40, allFlaskSlots, "持续时间内移动速度提高 40%" },
        new object[] { 22, "utility_granite",     "坚岩药剂",           3, 18, 1, 2,   0,   0, 4000, 60, 30, false,   0, 2,1500, allFlaskSlots, "持续时间内获得 +1500 护甲" },
        new object[] { 23, "utility_jade",        "翠玉药剂",           3, 27, 1, 2,   0,   0, 4000, 60, 30, false,   0, 3,1500, allFlaskSlots, "持续时间内获得 +1500 闪避" },
        new object[] { 24, "utility_ruby",        "红玉药剂",           3, 18, 1, 2,   0,   0, 5000, 50, 30, false,   0, 4,  40, allFlaskSlots, "持续时间内火焰抗性 +40%" },
        new object[] { 25, "utility_sapphire",    "蓝玉药剂",           3, 18, 1, 2,   0,   0, 5000, 50, 30, false,   0, 5,  40, allFlaskSlots, "持续时间内冰霜抗性 +40%" },
        new object[] { 26, "utility_topaz",       "黄玉药剂",           3, 18, 1, 2,   0,   0, 5000, 50, 30, false,   0, 6,  40, allFlaskSlots, "持续时间内闪电抗性 +40%" },
        new object[] { 27, "utility_amethyst",    "紫晶药剂",           3, 18, 1, 2,   0,   0, 5000, 50, 30, false,   0, 7,  35, allFlaskSlots, "持续时间内混沌抗性 +35%" },
        new object[] { 28, "utility_basalt",      "玄武岩药剂",         3, 27, 1, 2,   0,   0, 4000, 40, 40, false,   0, 8,  20, allFlaskSlots, "持续时间内承受的物理伤害额外降低 20%" },
        new object[] { 29, "utility_sulphur",     "硫磺药剂",           3, 35, 1, 2,   0,   0, 4000, 50, 30, false,   0, 9,   1, allFlaskSlots, "使用时制造奉献地面，持续 4 秒" },
        new object[] { 30, "utility_quartz",      "石英药剂",           3, 27, 1, 2,   0,   0, 4000, 50, 30, false,   0,10,   1, allFlaskSlots, "持续时间内获得穿相效果" },
        new object[] { 31, "utility_silver",      "白银药剂",           3, 40, 1, 2,   0,   0, 4000, 40, 40, false,   0,11,   1, allFlaskSlots, "持续时间内获得猛攻" },
    });
}

pkg.Save();
File.Copy(outputPath, legacyOutputPath, true);
Console.WriteLine($"已更新: {outputPath}");
Console.WriteLine($"已同步: {legacyOutputPath}");
