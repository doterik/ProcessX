using System.Globalization;

namespace Zx;

internal static class EscapeFormattableString
{
    internal static string Escape(FormattableString formattableString)
    {
        // Not already escaped?
        if (!(formattableString.Format.StartsWith('"') && formattableString.Format.EndsWith('"')))
        {
            var args = formattableString.GetArguments(); // Returns inner object[] field, it can modify.

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] is string s)
                {
                    args[i] = $@"""{s.Replace(@"""", @"\""", StringComparison.Ordinal)}"""; // Poor logic...
                }
            }
        }

        return formattableString.ToString(CultureInfo.InvariantCulture);
    }
}
