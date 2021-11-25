namespace Fergun.APIs.AIDungeon
{
    public interface IAiDungeonRequest<TVariables>
    {
        string Query { get; set; }

        TVariables Variables { get; set; }
    }
}