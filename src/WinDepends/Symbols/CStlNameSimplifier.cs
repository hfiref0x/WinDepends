/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CSTLNAMESIMPLIFIER.CS
*
*  VERSION:     1.00
*
*  DATE:        03 Jun 2026
*
*  Collapses the canonical (default-argument) forms produced by the MSVC
*  demangler into their conventional STL spellings, e.g.
*
*    std::basic_string<char,std::char_traits<char>,std::allocator<char> >
*       -> std::string
*    std::map<int,T,std::less<int>,std::allocator<std::pair<int const ,T> > >
*       -> std::map<int,T>
*    std::vector<T,std::allocator<T> >
*       -> std::vector<T>
*
*  Only the default-value template forms of the standard library are matched;
*  user templates and non-default arguments are left untouched. The transform
*  is bracket-aware (it walks balanced angle brackets) because regular
*  expressions cannot reliably split nested template argument lists.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Text;

namespace WinDepends;

/// <summary>
/// Folds MSVC default template-argument forms of the C++ standard library into
/// their conventional typedef spellings. Intended purely for display.
/// </summary>
internal static class CStlNameSimplifier
{
    /// <summary>
    /// Returns <paramref name="name"/> with recognized STL default-argument
    /// template forms collapsed. On any unexpected input the original string is
    /// returned unchanged so the feature can never corrupt a displayed name.
    /// </summary>
    public static string Simplify(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf('<') < 0)
        {
            return name;
        }

        try
        {
            return Transform(name);
        }
        catch
        {
            return name;
        }
    }

    /// <summary>
    /// Recursively rewrites every template-id found in <paramref name="s"/>.
    /// Inner argument lists are transformed before the enclosing template is
    /// folded, so nested defaults collapse bottom-up.
    /// </summary>
    private static string Transform(string s)
    {
        var sb = new StringBuilder(s.Length);
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];

            if (c == '<')
            {
                string ident = PeekLastIdentifier(sb);

                // Skip operator<, operator<<, lambdas (<lambda_..>) and anything
                // not preceded by a real identifier - treat '<' literally.
                if (ident.Length == 0 || ident.EndsWith("operator", StringComparison.Ordinal))
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                int end = FindMatchingAngle(s, i);
                if (end < 0)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                string inner = s.Substring(i + 1, end - i - 1);
                List<string> args = SplitTopLevel(Transform(inner));

                sb.Length -= ident.Length;          // remove identifier, re-emit folded
                sb.Append(Fold(ident, args));
                i = end + 1;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies the STL folding rules for a single template-id whose argument
    /// list has already been simplified. Unrecognized templates are rebuilt
    /// verbatim.
    /// </summary>
    private static string Fold(string ident, List<string> args)
    {
        switch (ident)
        {
            case "std::basic_string":
                // <CharT, char_traits<CharT>, allocator<CharT>>
                if (args.Count == 3 &&
                    IsSpecializationOf(args[1], "std::char_traits") &&
                    IsSpecializationOf(args[2], "std::allocator"))
                {
                    string alias = StringAlias(args[0], isView: false);
                    if (alias != null) return alias;
                }
                break;

            case "std::basic_string_view":
                // <CharT, char_traits<CharT>>
                if (args.Count == 2 &&
                    IsSpecializationOf(args[1], "std::char_traits"))
                {
                    string alias = StringAlias(args[0], isView: true);
                    if (alias != null) return alias;
                }
                break;

            case "std::vector":
            case "std::deque":
            case "std::list":
            case "std::forward_list":
                // <T, allocator<T>>
                if (args.Count == 2 && IsSpecializationOf(args[1], "std::allocator"))
                {
                    return Rebuild(ident, args[0]);
                }
                break;

            case "std::set":
            case "std::multiset":
                // <Key, less<Key>, allocator<Key>>
                if (args.Count == 3 &&
                    IsSpecializationOf(args[1], "std::less") &&
                    IsSpecializationOf(args[2], "std::allocator"))
                {
                    return Rebuild(ident, args[0]);
                }
                break;

            case "std::map":
            case "std::multimap":
                // <Key, T, less<Key>, allocator<pair<const Key, T>>>
                if (args.Count == 4 &&
                    IsSpecializationOf(args[2], "std::less") &&
                    IsSpecializationOf(args[3], "std::allocator"))
                {
                    return Rebuild(ident, args[0], args[1]);
                }
                break;

            case "std::unordered_set":
            case "std::unordered_multiset":
                // <Key, hash<Key>, equal_to<Key>, allocator<Key>>
                if (args.Count == 4 &&
                    IsSpecializationOf(args[1], "std::hash") &&
                    IsSpecializationOf(args[2], "std::equal_to") &&
                    IsSpecializationOf(args[3], "std::allocator"))
                {
                    return Rebuild(ident, args[0]);
                }
                break;

            case "std::unordered_map":
            case "std::unordered_multimap":
                // <Key, T, hash<Key>, equal_to<Key>, allocator<pair<const Key, T>>>
                if (args.Count == 5 &&
                    IsSpecializationOf(args[2], "std::hash") &&
                    IsSpecializationOf(args[3], "std::equal_to") &&
                    IsSpecializationOf(args[4], "std::allocator"))
                {
                    return Rebuild(ident, args[0], args[1]);
                }
                break;

            case "std::unique_ptr":
                // <T, default_delete<T>>
                if (args.Count == 2 && IsSpecializationOf(args[1], "std::default_delete"))
                {
                    return Rebuild(ident, args[0]);
                }
                break;
        }

        return Rebuild(ident, args);
    }

    /// <summary>
    /// Maps a character element type to the std::string / std::string_view family
    /// alias, or null when the element type is not a known character type.
    /// </summary>
    private static string StringAlias(string element, bool isView)
    {
        switch (StripLeadingKeywords(element).Trim())
        {
            case "char": return isView ? "std::string_view" : "std::string";
            case "wchar_t": return isView ? "std::wstring_view" : "std::wstring";
            case "char8_t": return isView ? "std::u8string_view" : "std::u8string";
            case "char16_t": return isView ? "std::u16string_view" : "std::u16string";
            case "char32_t": return isView ? "std::u32string_view" : "std::u32string";
            default: return null;
        }
    }

    /// <summary>
    /// True when <paramref name="arg"/> is a specialization of
    /// <paramref name="template"/> (e.g. "std::allocator&lt;...&gt;").
    /// </summary>
    private static bool IsSpecializationOf(string arg, string template)
    {
        arg = StripLeadingKeywords(arg).TrimStart();
        return arg.StartsWith(template, StringComparison.Ordinal) &&
               arg.Length > template.Length &&
               arg[template.Length] == '<';
    }

    private static string Rebuild(string ident, params string[] args)
        => Rebuild(ident, (IReadOnlyList<string>)args);

    /// <summary>
    /// Reconstructs "ident&lt;args&gt;" keeping the demangler convention of a
    /// space before a closing angle bracket that follows another '&gt;'.
    /// </summary>
    private static string Rebuild(string ident, IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ident;
        }

        var sb = new StringBuilder(ident.Length + 16);
        sb.Append(ident).Append('<');

        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(args[i]);
        }

        if (args[args.Count - 1].EndsWith(">", StringComparison.Ordinal))
        {
            sb.Append(' ');
        }
        sb.Append('>');

        return sb.ToString();
    }

    /// <summary>
    /// Splits a template argument list on top-level commas, ignoring commas
    /// nested inside angle brackets, parentheses or square brackets.
    /// </summary>
    private static List<string> SplitTopLevel(string s)
    {
        var result = new List<string>();
        int depth = 0, start = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '<':
                case '(':
                case '[':
                    depth++;
                    break;
                case '>':
                case ')':
                case ']':
                    if (depth > 0) depth--;
                    break;
                case ',':
                    if (depth == 0)
                    {
                        result.Add(s.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                    break;
            }
        }

        result.Add(s.Substring(start).Trim());
        return result;
    }

    /// <summary>
    /// Returns the index of the '&gt;' that matches the '&lt;' at
    /// <paramref name="open"/>, or -1 when the brackets are unbalanced.
    /// </summary>
    private static int FindMatchingAngle(string s, int open)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns the identifier characters immediately preceding the current end
    /// of <paramref name="sb"/> without modifying it.
    /// </summary>
    private static string PeekLastIdentifier(StringBuilder sb)
    {
        int end = sb.Length;
        int i = end - 1;
        while (i >= 0 && IsIdentifierChar(sb[i]))
        {
            i--;
        }
        return sb.ToString(i + 1, end - (i + 1));
    }

    private static bool IsIdentifierChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == ':';

    /// <summary>
    /// Strips MSVC type keywords ("class ", "struct ", "enum ", "union ") that
    /// may prefix a type when the demangler is not run with NoMsKeyWords.
    /// </summary>
    private static string StripLeadingKeywords(string s)
    {
        s = s.TrimStart();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (string kw in s_typeKeywords)
            {
                if (s.StartsWith(kw, StringComparison.Ordinal))
                {
                    s = s.Substring(kw.Length).TrimStart();
                    changed = true;
                }
            }
        }
        return s;
    }

    private static readonly string[] s_typeKeywords =
        ["class ", "struct ", "enum ", "union "];
}
