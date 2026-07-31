using Hexara.Domain.Board;
using Hexara.Domain.Game;

namespace Hexara.Web.ViewModels;

/// <summary>
/// اعدادِ صفحه‌ی قوانین.
///
/// **هیچ عددی در خودِ صفحه نوشته نمی‌شود.** همه از همان ثابت‌هایی می‌آیند که موتور
/// بازی با آن‌ها کار می‌کند، پس اگر روزی هزینه‌ی شهر یا حدِ نصابِ ارتش عوض شود،
/// صفحه‌ی قوانین همان لحظه درست می‌شود و کسی یادش نمی‌رود به‌روزش کند. همان
/// قاعده‌ای که برای جدول هزینه در رابط بازی هم گذاشتیم.
/// </summary>
public sealed class RulesViewModel
{
    /// <summary>پیش‌فرض‌های بازی؛ اتاق می‌تواند بعضی‌شان را عوض کند.</summary>
    public GameOptions Defaults { get; } = new() { PlayerCount = 4 };

    public IReadOnlyList<(string LabelKey, IReadOnlyDictionary<Resource, int> Cost)> Costs { get; } =
    [
        ("game.buildRoad", BuildCosts.Road),
        ("game.buildSettlement", BuildCosts.Settlement),
        ("game.buildCity", BuildCosts.City),
        ("game.buyCard", BuildCosts.DevelopmentCard)
    ];

    /// <summary>کارت‌های توسعه به ترتیبی که در دسته‌اند.</summary>
    public IReadOnlyList<DevelopmentCard> Cards { get; } = [.. Enum.GetValues<DevelopmentCard>()];
}
