#pragma warning disable CA1307  // Specify StringComparison for clarity.
#pragma warning disable MA0001  // StringComparison is missing.
#pragma warning disable MA0011  // IFormatProvider is missing.

namespace Zx;

internal static class EscapeFormattableString
{
    internal static string Escape(FormattableString formattableString)
    {
        // Already escaped.
        if (formattableString.Format.StartsWith('"') && formattableString.Format.EndsWith('"'))
        {
            return formattableString.ToString();
        }

        // GetArguments returns inner object[] field, it can modify.
        var args = formattableString.GetArguments();

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is string s)
            {
                args[i] = $@"""{s.Replace(@"""", @"\""")}"""; // Poor logic...
            }
        }

        return formattableString.ToString();
    }
}
