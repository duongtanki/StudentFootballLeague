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
            
            string badgeClass;
            string displayText;

            switch (Status?.ToLower())
            {
                case "scheduled":
                    badgeClass = "bg-secondary text-white";
                    displayText = "Sắp diễn ra";
                    break;
                case "inprogress":
                    badgeClass = "bg-danger text-white animate-pulse";
                    displayText = "● Đang thi đấu";
                    break;
                case "completed":
                    badgeClass = "bg-success text-white";
                    displayText = "Đã kết thúc";
                    break;
                case "postponed":
                    badgeClass = "bg-warning text-dark";
                    displayText = "Tạm hoãn";
                    break;
                case "cancelled":
                    badgeClass = "bg-dark text-white";
                    displayText = "Đã hủy";
                    break;
                default:
                    badgeClass = "bg-light text-dark";
                    displayText = Status ?? "Chưa rõ";
                    break;
            }

            output.Attributes.SetAttribute("class", $"badge {badgeClass} px-2 py-1");
            output.Content.SetContent(displayText);
        }
    }
}
