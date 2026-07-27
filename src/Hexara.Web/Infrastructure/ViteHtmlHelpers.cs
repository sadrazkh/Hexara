using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Web.Infrastructure;

public static class ViteHtmlHelpers
{
    /// <summary>
    /// تگ‌های <c>script</c> و <c>link</c> ورودی ویت را تولید می‌کند.
    /// در حالت dev server، کلاینت HMR هم تزریق می‌شود.
    /// </summary>
    public static IHtmlContent ViteEntry(this IHtmlHelper html, string entry)
    {
        var context = html.ViewContext.HttpContext;
        var manifest = context.RequestServices.GetRequiredService<ViteManifest>();

        // اسکریپت‌های ماژولی هم nonce می‌گیرند تا CSP بدون unsafe-inline کار کند.
        var nonce = $" nonce=\"{context.CspNonce()}\"";
        var (script, styles) = manifest.Resolve(entry);
        var builder = new HtmlContentBuilder();

        if (manifest.DevServerEnabled)
        {
            builder.AppendHtml($"<script type=\"module\" src=\"{manifest.DevServerUrl}/@vite/client\"></script>\n");
        }

        foreach (var style in styles)
        {
            builder.AppendHtml($"<link rel=\"stylesheet\" href=\"{style}\" />\n");
        }

        if (!string.IsNullOrEmpty(script))
        {
            builder.AppendHtml($"<script type=\"module\" src=\"{script}\"></script>\n");
        }

        return builder;
    }
}
