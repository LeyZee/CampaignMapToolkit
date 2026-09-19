namespace CAIME
{
    /// <summary>
    /// Where CAIME loads its database tables from. <see cref="AssemblyKit"/> is the original,
    /// default behaviour; <see cref="RPFM"/> prepares the tables from a .pack file first.
    /// </summary>
    public enum DatabaseSource
    {
        AssemblyKit = 0,
        RPFM        = 1,
    }
}
