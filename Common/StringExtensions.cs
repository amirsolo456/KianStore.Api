namespace KianStore.Api.Common;

public static class StringExtensions
{
    public static string ToPersianChars(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return input.Trim()
            .Replace('ي', 'ی')
            .Replace('ك', 'ک');
    }

    public static string ToArabicChars(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return input.Trim()
            .Replace('ی', 'ي')
            .Replace('ک', 'ك');
    }
}
