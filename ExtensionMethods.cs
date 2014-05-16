using System;
using System.Collections.Generic;
using System.Linq;

namespace MeasureScheduleDuration
{
    public static class ExtensionMethods
    {
        public static DateTime Floor(this DateTime dt, TimeSpan segment)
        {
            var date = dt.Date;
            var time = dt.TimeOfDay;
            var parts = (int)(time.Ticks / segment.Ticks);
            return date + TimeSpan.FromTicks(segment.Ticks * parts);
        }

        public static string Longest(this IEnumerable<string> src)
        {
            return src.OrderByDescending(v => v.Length)
                .First();
        }

        public static IEnumerable<TResult> SelectOrSkip<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, TResult> fn) where TResult : class
        {
            return src.SelectOrNull(fn).Where(i => i != null);
        }

        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, TResult> fn) where TResult : class
        {
            return src.Select(fn).Where(i => i != null);
        }

        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, int, TResult> fn) where TResult : class
        {
            return src.Select(fn).Where(i => i != null);
        }

        public static
            IEnumerable<TResult> SelectOrNull<TSource, TResult>(this IEnumerable<TSource> src,
                Func<TSource, TResult> fn) where TResult:class
        {
            return
                src.Select(i =>
                {
                    try
                    {
                        return
                            fn(i);
                    }
                    catch
                    {
                        return null;
                    }
                }
                    );

        }
    }
}