using System.Collections.Frozen;
using Microsoft.AspNetCore.Html;

namespace Arrendia.Web.Models;

public static class Iconos
{
    private static readonly FrozenDictionary<string, string> Trazos = new Dictionary<string, string>
    {
        ["chart"] = "<path d=\"M4 20V4M4 20h16\"/><path d=\"M8 16v-4M12 16V8M16 16v-6\"/>",
        ["building"] = "<rect x=\"5\" y=\"3\" width=\"14\" height=\"18\" rx=\"1.5\"/><path d=\"M9 8h2M13 8h2M9 12h2M13 12h2M10 21v-4h4v4\"/>",
        ["door"] = "<path d=\"M6 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17\"/><path d=\"M4 21h16\"/><circle cx=\"14.5\" cy=\"12.5\" r=\".7\"/>",
        ["users"] = "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 20c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M17 14.5c2.4 0 4 1.7 4 4.5\"/>",
        ["file"] = "<path d=\"M7 3h7l5 5v12a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z\"/><path d=\"M14 3v5h5M9 13h6M9 17h6\"/>",
        ["wrench"] = "<path d=\"M15 5a4 4 0 0 0-4.6 5.2L4 16.6V20h3.4l6.4-6.4A4 4 0 0 0 19 9l-2.6 2.6-2.5-.5-.5-2.5z\"/>",
        ["shield"] = "<path d=\"M12 3 5 6v5.5c0 4.4 3 7.6 7 9.5 4-1.9 7-5.1 7-9.5V6z\"/><path d=\"m9 12 2.2 2.2L15.5 10\"/>",
        ["sliders"] = "<path d=\"M4 7h10M18 7h2M4 17h2M10 17h10\"/><circle cx=\"16\" cy=\"7\" r=\"2\"/><circle cx=\"8\" cy=\"17\" r=\"2\"/>",
        ["receipt"] = "<path d=\"M6 3h12v18l-3-2-3 2-3-2-3 2z\"/><path d=\"M9 8h6M9 12h6\"/>",
        ["coin"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M14.5 9.2c-.5-.8-1.4-1.2-2.5-1.2-1.4 0-2.5.7-2.5 1.8 0 2.7 5.2 1.3 5.2 4 0 1.1-1.1 1.9-2.7 1.9-1.1 0-2.1-.5-2.7-1.4M12 6.5V8M12 16v1.5\"/>",
        ["alert"] = "<path d=\"M12 3.5 21 19H3z\"/><path d=\"M12 10v4M12 17h.01\"/>",
        ["qr"] = "<rect x=\"4\" y=\"4\" width=\"6\" height=\"6\" rx=\"1\"/><rect x=\"14\" y=\"4\" width=\"6\" height=\"6\" rx=\"1\"/><rect x=\"4\" y=\"14\" width=\"6\" height=\"6\" rx=\"1\"/><path d=\"M14 14h2v2M20 14v.01M14 20h2M18 18v2h2\"/>",
        ["mail"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m3.5 7 8.5 6 8.5-6\"/>",
        ["history"] = "<path d=\"M4 12a8 8 0 1 0 2.5-5.8L4 8.5\"/><path d=\"M4 4v4.5h4.5M12 8v4l3 2\"/>",
        ["menu"] = "<path d=\"M4 7h16M4 12h16M4 17h16\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6 7 7M17 17l1.4 1.4M5.6 18.4 7 17M17 7l1.4-1.4\"/>",
        ["moon"] = "<path d=\"M20 14.5A8.5 8.5 0 0 1 9.5 4 8.5 8.5 0 1 0 20 14.5z\"/>",
        ["monitor"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M8 20h8M12 16v4\"/>",
        ["logout"] = "<path d=\"M10 4H6a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h4M15 8l4 4-4 4M19 12H9\"/>",
        ["eye"] = "<path d=\"M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z\"/><circle cx=\"12\" cy=\"12\" r=\"2.8\"/>",
        ["lock"] = "<rect x=\"5\" y=\"11\" width=\"14\" height=\"10\" rx=\"2\"/><path d=\"M8 11V8a4 4 0 0 1 8 0v3\"/>",
        ["check"] = "<path d=\"m5 12.5 4.5 4.5L19 7.5\"/>",
        ["chevL"] = "<path d=\"m15 6-6 6 6 6\"/>",
        ["home"] = "<path d=\"M3 11.5 12 4l9 7.5\"/><path d=\"M5 10v10h14V10\"/><path d=\"M10 20v-6h4v6\"/>",
        ["card"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"M3 10h18M7 15h3\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["edit"] = "<path d=\"M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z\"/>",
        ["pin"] = "<path d=\"M12 21s7-6.2 7-11.5a7 7 0 0 0-14 0C5 14.8 12 21 12 21z\"/><circle cx=\"12\" cy=\"9.5\" r=\"2.5\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"m16 16 4.5 4.5\"/>",
        ["sidebar"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M9 4v16\"/>",
    }.ToFrozenDictionary();

    public static HtmlString Svg(string nombre, string clase = "") =>
        new($"<svg class=\"ic {clase}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{Trazos.GetValueOrDefault(nombre, "")}</svg>");

    public static readonly HtmlString Logo = new(
        "<span class=\"logo\"><svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"M12 3 3.5 21h4.2l1.4-3.4h5.8l1.4 3.4h4.2zM10.4 14.2 12 10.3l1.6 3.9z\" fill-rule=\"evenodd\"/></svg></span>");
}
