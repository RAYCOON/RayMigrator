namespace Raycoon.RayMigrator.Database.Common;

public class DalSpecificProperties
{
    public string SqlBlockDelimiter = string.Empty;
    public string SqlMultiLineCommentStart = string.Empty;
    public string SqlMultiLineCommentEnd = string.Empty;
    public bool SupportsSchema;
    public bool SupportsTransactionalDdl = true;
    public string IdentifierQuoteStart = string.Empty;
    public string IdentifierQuoteEnd = string.Empty;
    public string DefaultSchema = string.Empty;
    public bool FoldsUnquotedIdentifiersToLower;
}