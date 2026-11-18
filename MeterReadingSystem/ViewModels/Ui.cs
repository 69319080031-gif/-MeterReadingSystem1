using System.Net;
using MeterReadingSystem.Models;
using Microsoft.AspNetCore.Html;

namespace MeterReadingSystem.ViewModels;

/// <summary>Small HTML snippets reused across views (badges).</summary>
public static class Ui
{
    public static IHtmlContent TypeBadge(MeterType t) => t == MeterType.Water
        ? new HtmlString("<span class=\"badge-soft badge-water\"><i class=\"bi bi-droplet-fill\"></i>น้ำ</span>")
        : new HtmlString("<span class=\"badge-soft badge-elec\"><i class=\"bi bi-lightning-charge-fill\"></i>ไฟฟ้า</span>");

    public static IHtmlContent StatusBadge(MeterStatus s)
    {
        var cls = s switch
        {
            MeterStatus.Active => "badge-active",
            MeterStatus.Maintenance => "badge-maint",
            _ => "badge-inactive"
        };
        return new HtmlString($"<span class=\"badge-soft {cls}\">{WebUtility.HtmlEncode(MeterLabels.StatusName(s))}</span>");
    }
}
