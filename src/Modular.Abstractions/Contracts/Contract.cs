using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace Modular.Abstractions.Contracts;

public abstract class Contract<T> : IContract where T : class
{
    private readonly ISet<string> _required = new HashSet<string>();
    public Type Type { get; } = typeof(T);
    public IEnumerable<string> Required => _required;

    protected void Require(Expression<Func<T, object>> expression) => _required.Add(GetName(expression));

    protected void Ignore(Expression<Func<T, object>> expression) => _required.Remove(GetName(expression));

    protected string GetName(Expression<Func<T, object>> expression)
    {
        if (!(expression.Body is MemberExpression memberExpression))
        {
            memberExpression = ((UnaryExpression)expression.Body).Operand as MemberExpression;
        }

        if (memberExpression is null)
        {
            throw new InvalidOperationException("Invalid member expression.");
        }

        IEnumerable<string> parts = expression.ToString().Split(",")[0].Split(".").Skip(1);
        string name = string.Join(".", parts);

        return name;
    }

    protected void RequireAll() => RequireAll(typeof(T));

    private void RequireAll(Type type, string parent = null)
    {
        object originalContract = FormatterServices.GetUninitializedObject(type);
        Type originalContractType = originalContract.GetType();
        foreach (PropertyInfo property in originalContractType.GetProperties())
        {
            string propertyName = string.IsNullOrWhiteSpace(parent) ? property.Name : $"{parent}.{property.Name}";
            _required.Add(propertyName);
            if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                RequireAll(property.PropertyType, propertyName);
            }
        }
    }

    protected void IgnoreAll() => _required.Clear();
}