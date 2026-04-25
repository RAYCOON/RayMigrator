namespace Raycoon.RayMigrator.Database.Common;

public class DalParameterList
{
    private readonly Dictionary<string, DalParameter> _paramDict = new();

    /// <summary>
    /// Add or update value.
    /// </summary>
    /// <param name="parameterValue">The value of the parameter.</param>
    /// <returns>True if parameter was successfully added, otherwise false.</returns>
    public void AddParameter(DalParameter parameterValue)
    {
        string parameterName = parameterValue.ParameterName;
        _paramDict.Add(parameterName, parameterValue);
    }

    /// <summary>
    /// Tries to obtain the value of a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="parameterValue">The value of the parameter.</param>
    /// <returns>True if parameter was successfully added, otherwise false.</returns>
    public bool TryGetValue(string parameterName, out DalParameter? parameterValue)
    {
        if (_paramDict.TryGetValue(parameterName, out parameterValue))
        {
            return true;
        }

        parameterValue = null;
        return false;
    }

    /// <summary>
    /// All parameters within the internal ParameterDictionary.
    /// </summary>
    /// <returns>Returns all parameters as IEnumerable of KeyValue-Pairs.</returns>
    public IEnumerable<KeyValuePair<string, DalParameter>> GetAllParameters() => _paramDict;
}