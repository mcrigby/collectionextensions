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
                .ToHashSet();

            return leftList.LeftJoin(rightList, outerKeySelector, innerKeySelector, resultSelector)
                .Concat(rightList
                    .Where(right => !leftListKeys.Contains(innerKeySelector(right)))
                    .Select(right => resultSelector(default, right)));
        }

        public static IEnumerable<TResult> TrySelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            foreach (var item in source)
            {
                TResult result;
                try
                {
                    result = selector(item);
                }
                catch
                {
                    result = default;
                }
                yield return result;
            }
        }
    }
}

namespace System.Linq
{
    public static class Extensions
    {
        public static IOrderedQueryable<TEntity> OrderBy<TEntity>(this IQueryable<TEntity> source, IEnumerable<string> properties, bool descending = false)
        {
            properties ??= Enumerable.Empty<string>();

            var iComparableType = typeof(IComparable);

            var entityType = typeof(TEntity);
            var entityParameter = Expression.Parameter(entityType);

            var lambdaExpressions = properties
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Select(propertyName => entityType.GetProperty(propertyName,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
                .Where(property => property != null && iComparableType.IsAssignableFrom(property.PropertyType))
                .Select(filteredProperty => Expression.MakeMemberAccess(entityParameter, filteredProperty))
                .Select(memberAccessExpression => Expression.Convert(memberAccessExpression, typeof(object)))
                .Select(convertedExpression =>
                    Expression.Lambda<Func<TEntity, object>>(convertedExpression, entityParameter))
                .ToArray();

            if (!descending)
                return lambdaExpressions
                    .Aggregate(source.OrderBy(x => 0), (current, lambdaExpression) => current.ThenBy(lambdaExpression));
            else
                return lambdaExpressions
                    .Aggregate(source.OrderByDescending(x => 0), (current, lambdaExpression) => current.ThenByDescending(lambdaExpression));
        }

        public static IOrderedQueryable<TEntity> OrderBy<TEntity, TNavigationProperty>(this IQueryable<TEntity> source,
            Expression<Func<TEntity, TNavigationProperty>> navigationProperty, IEnumerable<string> properties, bool descending = false)
        {
            properties ??= Enumerable.Empty<string>();

            var iComparableType = typeof(IComparable);

            var entityType = typeof(TEntity);
            var entityParameter = Expression.Parameter(entityType);

            var navigationProperyType = typeof(TNavigationProperty);
            var navigationPropertyMemberInfo = ((MemberExpression)navigationProperty.Body).Member;
            var navigationPropertyParameter = Expression.MakeMemberAccess(entityParameter, navigationPropertyMemberInfo);

            var lambdaExpressions = properties
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Select(propertyName => navigationProperyType.GetProperty(propertyName,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
                .Where(property => property != null && iComparableType.IsAssignableFrom(property.PropertyType))
                .Select(filteredProperty => Expression.MakeMemberAccess(navigationPropertyParameter, filteredProperty))
                .Select(memberAccessExpression => Expression.Convert(memberAccessExpression, typeof(object)))
                .Select(convertedExpression =>
                    Expression.Lambda<Func<TEntity, object>>(convertedExpression, entityParameter))
                .ToArray();

            if (!descending)
                return lambdaExpressions
                    .Aggregate(source.OrderBy(x => 0), (current, lambdaExpression) => current.ThenBy(lambdaExpression));
            else
                return lambdaExpressions
                    .Aggregate(source.OrderByDescending(x => 0), (current, lambdaExpression) => current.ThenByDescending(lambdaExpression));
        }

        public static bool Exists<TResult>(this IQueryable<TResult> source, Expression<Func<TResult, bool>> predicate) =>
            source.Any(predicate);
    }
}
