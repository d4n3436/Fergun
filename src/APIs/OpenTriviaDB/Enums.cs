namespace Fergun.APIs.OpenTriviaDB
{
    public enum QuestionCategory
    {
        Any,
        GeneralKnowledge = 9,
        Books,
        Film,
        Music,
        MusicalsAndTheatres,
        Television,
        VideoGames,
        BoardGames,
        ScienceAndNature,
        Computers,
        Mathematics,
        Mythology,
        Sports,
        Geography,
        History,
        Politics,
        Art,
        Celebrities,
        Animals,
        Vehicles,
        Comics,
        Gadgets,
        AnimeAndManga,
        CartoonsAndAnimations
    }

    public enum QuestionDifficulty
    {
        Any,
        Easy,
        Medium,
        Hard
    }

    public enum QuestionType
    {
        Any,
        Multiple,
        Boolean
    }

    public enum ResponseEncoding
    {
        // Default Encoding (HTML Codes)
        Default, // Unlike the other default values on the other enums, this can't really be passed as an argument because it returns response code 2.
        urlLegacy,
        url3986,
        base64
    }

    public enum TokenCommand
    {
        Request,
        Reset
    }
}