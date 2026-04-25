namespace Raycoon.RayMigrator.Database.Common;

public class DalParameter
{
    public string ParameterName { get; set; }
    public object? ParameterValue { get; set; }
    public Type ParameterType { get; set; }

    public DalParameter(string name, object? value, Type type)
    {
        ParameterName = name;
        ParameterValue = value;
        ParameterType = type;
    }

    public override string ToString()
    {
        return (ParameterValue ?? "NULL") + $" ({ParameterType})";
    }
}