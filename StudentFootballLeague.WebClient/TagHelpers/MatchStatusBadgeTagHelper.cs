using Microsoft.AspNetCore.Razor.TagHelpers;

namespace StudentFootballLeague.WebClient.TagHelpers
{
    [HtmlTargetElement("match-status")]
    public class MatchStatusBadgeTagHelper : TagHelper
    {
        [HtmlAttributeName("status")]
        public string? Status { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "span";

            string modifierClass;
            string innerHtml;

            switch (Status?.ToLower())
            {
                case "scheduled":
                    modifierClass = "apex-badge--scheduled";
                    innerHtml = "Sắp diễn ra";
                    break;
                case "inprogress":
                    modifierClass = "apex-badge--live";
                    innerHtml = "<span class=\"apex-pulse-dot\"></span>Đang thi đấu";
                    break;
                case "completed":
                    modifierClass = "apex-badge--completed";
                    innerHtml = "Đã kết thúc";
                    break;
                case "postponed":
                    modifierClass = "apex-badge--postponed";
                    innerHtml = "Tạm hoãn";
                    break;
                case "cancelled":
                    modifierClass = "apex-badge--cancelled";
                    innerHtml = "Đã hủy";
                    break;
                default:
                    modifierClass = "apex-badge--scheduled";
                    innerHtml = System.Net.WebUtility.HtmlEncode(Status ?? "Chưa rõ");
                    break;
            }

            output.Attributes.SetAttribute("class", $"apex-badge {modifierClass}");
            output.Content.SetHtmlContent(innerHtml);
        }
    }
}
