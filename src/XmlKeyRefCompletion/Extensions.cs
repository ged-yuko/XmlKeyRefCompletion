using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Package = Microsoft.XmlEditor.Package;

namespace XmlKeyRefCompletion
{
    public static class Extensions
    {
        public static IVsTextView GetVsTextView(this ITextView textView)
        {
            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            IVsEditorAdaptersFactoryService editorFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            IVsTextView view = editorFactory.GetViewAdapter(textView);

            return view;
        }

        public static string TrimLength(this string str, int length)
        {
            return str.Length > length ? str.Substring(0, length) : str;
        }

        //public struct TestingSelectorResult<T>
        //{
        //    public readonly T item;

        //    private TestingSelectorResult(T item)
        //    {
        //        this.item = item;
        //    }

        //    public static implicit operator TestingSelectorResult<T>(T item)
        //    {
        //        return new TestingSelectorResult<T>(item);
        //    }            
        //}

        //public static IEnumerable<TRet> SelectWhere<T, TRet>(this IEnumerable<T> seq, Func<T, TestingSelectorResult<TRet>?> testingSelector)
        //{
        //    foreach (var item in seq)
        //    {
        //        var result = testingSelector(item);
        //        if (result.HasValue)
        //            yield return result.Value.item;
        //    }
        //}

        public static IEnumerable<TRet> SelectWhere<T, TRet>(this IEnumerable<T> seq, Func<T, bool> cond, Func<T, int, TRet> selector)
        {
            int counter = 0;
            foreach (var item in seq)
            {
                if (cond(item))
                    yield return selector(item, counter);

                counter++;
            }
        }

        public static bool All<T>(this IEnumerable<T> seq, Func<T, int, bool> cond)
        {
            int index = 0;
            foreach (var item in seq)
                if (!cond(item, index++))
                    return false;

            return true;
        }

        public static IEnumerable<T> FindCommonPrefix<T>(this IEnumerable<IEnumerable<T>> lines)
            where T : IEquatable<T>
        {
            return lines.Transpose().TakeWhile(cc => cc.All(c => c.Equals(cc.First()))).Select(c => c.First());
        }

        public static IEnumerable<IEnumerable<T>> Transpose<T>(this IEnumerable<IEnumerable<T>> source)
        {
            var enumerators = source.Select(e => e.GetEnumerator()).ToArray();
            try
            {
                while (enumerators.All(e => e.MoveNext()))
                {
                    yield return enumerators.Select(e => e.Current).ToArray();
                }
            }
            finally
            {
                Array.ForEach(enumerators, e => e.Dispose());
            }
        }

        public static bool HasFlag<T>(this T value, T flag)
            where T : struct
        {
            var o = value as Enum;
            var f = flag as Enum;

            return o.HasFlag(f);
        }

        public static bool IsEmpty(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsNotEmpty(this string str)
        {
            return !string.IsNullOrWhiteSpace(str);
        }

        public static string RawHexDump(this IEnumerable<byte> data, int colsCount = -1)
        {
            if (colsCount < 0)
            {
                return string.Join(" ", data.Select(n => Convert.ToString(n, 16).PadLeft(2, '0')));
            }
            else
            {
                var off = 0;
                return string.Join(Environment.NewLine, data.GroupBy(n => off++ / colsCount).Select(r => string.Join(" ", r.Select(n => Convert.ToString(n, 16).PadLeft(2, '0')))));
            }
        }

        public static string FormatCollectHexDump(this IEnumerable<byte> data)
        {
            var off = 0;
            return string.Join(Environment.NewLine, data.GroupBy(n => off++ / 16).Select(r => Convert.ToString(r.Key * 16, 16).PadLeft(4, '0') + ": " + string.Join(" ", r.Select(n => Convert.ToString(n, 16).PadLeft(2, '0')))));
        }

        public static string CollectTree<T>(this T node, Func<T, IEnumerable<T>> childsSelector, Func<T, string> nodeFormat)
        {
            var sb = new StringBuilder();
            CollectTreeImpl(sb, string.Empty, string.Empty, node, childsSelector, nodeFormat);
            return sb.ToString();
        }

        private static void CollectTreeImpl<T>(StringBuilder sb, string prefix, string childPrefix, T node, Func<T, IEnumerable<T>> childsSelector, Func<T, string> nodeFormat)
        {
            sb.Append(prefix).Append(" ").Append(nodeFormat(node)).AppendLine();

            var nodeChilds = childsSelector(node).ToArray();
            for (int i = 0; i < nodeChilds.Length; i++)
            {
                var item = nodeChilds[i];

                if (i < nodeChilds.Length - 1)
                    CollectTreeImpl(sb, childPrefix + "  ├─", childPrefix + "  │ ", item, childsSelector, nodeFormat);
                else
                    CollectTreeImpl(sb, childPrefix + "  └─", childPrefix + "    ", item, childsSelector, nodeFormat);
            }

            if (nodeChilds.Length > 0 && !childsSelector(nodeChilds.Last()).Any())
                sb.Append(childPrefix).AppendLine();
        }

        public static IEnumerable<T> Flatten<T>(this T node, Func<T, IEnumerable<T>> childsSelector)
        {
            yield return node;

            foreach (var child in childsSelector(node))
                foreach (var subitem in child.Flatten(childsSelector))
                    yield return subitem;
        }

        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> seq, Func<T, bool> separator)
        {
            using (var iter = seq.GetEnumerator())
            {
                while (iter.MoveNext())
                    yield return SplitImpl(iter, separator);
            }
        }

        private static IEnumerable<T> SplitImpl<T>(this IEnumerator<T> iter, Func<T, bool> separator)
        {
            do
            {
                var item = iter.Current;

                if (separator(item))
                    break;
                else
                    yield return item;
            }
            while (iter.MoveNext());
        }

        public static string ToBase64(this string str)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }

        public static string FromBase64(this string str)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }

        public static Exception GetFirstChanceExc(this Exception givenExc)
        {
            Exception retVal = givenExc;
            while (retVal.InnerException != null)
            {
                retVal = retVal.InnerException;
            }

            return retVal;
        }

        public static Exception GetRealException(this Exception givenExc)
        {
            Exception retVal = givenExc;
            while (retVal is TargetInvocationException)
            {
                retVal = retVal.InnerException;
            }

            return retVal;
        }

        public static ReadOnlyCollection<T> AsReadOnly<T>(this IList<T> items)
        {
            return new ReadOnlyCollection<T>(items);
        }

        public static bool HasCustomAttribute<T>(this MemberInfo info, bool inherit = false)
            where T : Attribute
        {
            return info.GetCustomAttributes(typeof(T), inherit).Length > 0;
        }

        public static bool TryGetCustomAttribute<T>(this MemberInfo info, out T attr, bool inherit = false)
            where T : Attribute
        {
            return null != (attr = info.GetCustomAttributes(typeof(T), inherit).OfType<T>().FirstOrDefault<T>());
        }

        public static bool IsValueDeclared<T>(this T value)
            where T : struct
        {
            return Enum.IsDefined(typeof(T), value);
        }

        public static T ParseEnum<T>(this string str, bool ignoreCase = true)
            where T : struct
        {
            return (T)Enum.Parse(typeof(T), str, ignoreCase);
        }

        public static T ParseEnumOrDefault<T>(this string str, T @default = default(T), bool ignoreCase = true)
            where T : struct
        {
            T ret;
            return Enum.TryParse(str, ignoreCase, out ret) ? ret : @default;
        }

        public static int IndexOf<T>(this IEnumerable<T> seq, Func<T, bool> cond)
        {
            int index = 0;
            foreach (var item in seq)
            {
                if (cond(item))
                    return index;

                index++;
            }

            return -1;
        }

        public static IEnumerable<T> Apply<T>(this IEnumerable<T> seq, Action<T> act)
        {
            foreach (var item in seq)
            {
                act(item);
                yield return item;
            }
        }

        public static void ForEach<T>(this IEnumerable<T> seq, Action<T> act)
        {
            foreach (var item in seq)
            {
                act(item);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> seq, Action<T, int> act)
        {
            var n = 0;
            foreach (var item in seq)
            {
                act(item, n);
                n++;
            }
        }

        public static void ForEach<T>(this Array array, Action<int[], T> act)
        {
            int[] indicies = new int[array.Rank];

            SetDimension<T>(array, indicies, 0, act);
        }

        private static void SetDimension<T>(Array array, int[] indicies, int dimension, Action<int[], T> act)
        {
            for (int i = 0; i <= array.GetUpperBound(dimension); i++)
            {
                indicies[dimension] = i;

                if (dimension < array.Rank - 1)
                    SetDimension<T>(array, indicies, dimension + 1, act);
                else
                    act(indicies, (T)array.GetValue(indicies));
            }
        }

        public static void SafeDispose(this IDisposable obj)
        {
            if (obj != null)
            {
                try
                {
                    obj.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                }
            }
        }

        public static string EscapeNewLines(this string str)
        {
            return str.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
