using System.Globalization;

namespace ClaudeLog.Web.Services;

public static class TitleGenerator
{
    public static string MakeTitle(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return string.Empty;

        var si = new StringInfo(question.Trim());
        int maxLength = 200;
        int len = Math.Min(si.LengthInTextElements, maxLength);
        var title = si.SubstringByTextElements(0, len);

        return si.LengthInTextElements > maxLength ? title + "…" : title;
    }
}
