using ClassIslandBot.Services.Webhooks;

namespace ClassIslandBot.Helpers;

public static class IssueBodyHelpers
{
    public static string ExtractIssue(string body, IEnumerable<string> tagNames)
    {
        return ExtractBetweenHeadings(body ?? "",
            tagNames.Any(x => x is IssueWebhookProcessorService.FeatureTagName or IssueWebhookProcessorService.ImprovementTagName)
                ? "### 背景与动机"
                : "### Bug 信息", 
            "### 最后一步");
    }
    
    public static string ExtractBetweenHeadings(string text, string startHeading, string endHeading)
    {
        // 分割文本为行数组，兼容不同换行符
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        
        var startIndex = -1;
        var endIndex = -1;

        // 查找开始标题
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals(startHeading, StringComparison.Ordinal))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1) return string.Empty; // 开始标题未找到

        // 从开始标题之后查找结束标题 "##最后一步"
        for (var i = lines.Length - 1; i > startIndex; i--)
        {
            if (lines[i].Trim().Equals(endHeading, StringComparison.Ordinal))
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1 || endIndex <= startIndex) return string.Empty; // 结束标题未找到或位置错误

        // 提取中间内容（排除两个标题行）
        var contentLines = lines
            .Skip(startIndex)
            .Take(endIndex - startIndex - 1)
            .ToArray();

        return string.Join(Environment.NewLine, contentLines);
    }
}