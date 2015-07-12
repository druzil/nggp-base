using System.Text;
using System;
namespace nJocLogic.util
{
    using System.Collections.Generic;
    using System.Linq;

    public static class Util
    {

        /** Take a string and escape backslashes and quotes.
         * 
         * @param str The string to escape.
         * @return The string in which all backslashes and quotes have been escaped.
         */
        public static string EscapeChars(string str)
        {
            var builder = new StringBuilder(str.Length + 6);

            foreach (char c in str)
            {
                // if we have to quote this, stick a backslash in front.
                if (c == '"' || c == '\\')
                    builder.Append('\\');

                builder.Append(c);
            }

            return builder.ToString();
        }


        public static string MakeIndent(int howMuch)
        {
            return MakeIndent(howMuch, "  ");
        }

        public static string MakeIndent(int howMuch, String indent)
        {
            var sb = new StringBuilder();

            while (howMuch-- > 0)
                sb.Append(indent);

            return sb.ToString();
        }

        public static long ExtractMatchID(String str)
        {
            if (str == null || !str.StartsWith("match."))
                return -1;
            long res;
            try
            {
                res = long.Parse(str.Substring(6));
            }
            catch (Exception)
            {
                res = -2;
            }
            return res;
        }

        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(emptyProduct,
                                      (accumulator, sequence) =>
                                        from accseq in accumulator
                                        from item in sequence
                                        select accseq.Concat(new[] { item }));
        }
    }
}
