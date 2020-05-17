using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace System.Collections.Generic
{
    public static class Extensions
    {
        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (left, right) => new { left, right = right.DefaultIfEmpty() })
                .SelectMany(joined => joined.right.Select(right => resultSelector(joined.left, right)));
        }

        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            return inner.LeftJoin(outer, innerKeySelector, outerKeySelector, (right, left) => resultSelector(left, right));
        }

        public static IEnumerable<TResult> FullJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            var leftList = outer.ToList();
            var rightList = inner.ToList();

            var leftListKeys = leftList
                .Select(outerKeySelector)
#if NETSTANDARD2_0
                .ToHashSet();
#else
                .ToHashSet();
#endif

            return leftList.LeftJoin(rightList, outerKeySelector, innerKeySelector, resultSelector)
                .Concat(rightList
                    .Where(right => !leftListKeys.Contains(innerKeySelector(right)))
                    .Select(right => resultSelector(default, right)));
        }

#if NETSTANDARD2_0
        [Obsolete("In the .NET framework and in NET core this method is available, " +
                  "however can't use it in .NET standard yet. When it's added, please remove this method")]
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null) => new HashSet<T>(source, comparer);
#endif
    }
}

namespace System.Linq
{
    public static class Extensions
    {
        public static IOrderedQueryable<TEntity> OrderBy<TEntity>(this IQueryable<TEntity> source, IEnumerable<string> properties)
        {
            var entityType = typeof(TEntity);
            var iComparableType = typeof(IComparable);
            var parameter = Expression.Parameter(entityType);
            var lambdaExpressions = properties
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Select(propertyName => entityType.GetProperty(propertyName,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
                .Where(property => property != null && iComparableType.IsAssignableFrom(property.PropertyType))
                .Select(filteredProperty => Expression.MakeMemberAccess(parameter, filteredProperty))
                .Select(memberAccessExpression => Expression.Convert(memberAccessExpression, iComparableType))
                .Select(convertedExpression =>
                    Expression.Lambda<Func<TEntity, IComparable>>(convertedExpression, parameter))
                .ToArray();

            var first = lambdaExpressions.FirstOrDefault();
            if (first == default(LambdaExpression))
                return source.OrderBy(x => 0);

            var result = source.OrderBy(first);
            result = lambdaExpressions.Skip(1)
                .Aggregate(result, (current, lambdaExpression) => current.ThenBy(lambdaExpression));

            return result;
        }
    }
}
