using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntry"/>
public record DictionaryEntry([property: JsonPropertyName("entry")] string Entry,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("entryVariants")] IReadOnlyList<string> EntryVariants,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [property: JsonPropertyName("homograph")] int? Homograph,
    [property: JsonPropertyName("pronunciation")] EntryPronunciation? Pronunciation,
    [property: JsonPropertyName("posBlocks")] IReadOnlyList<DictionaryEntryBlock> PartOfSpeechBlocks,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("supplementaryNotes")] IReadOnlyList<EntrySupplementaryNote> SupplementaryNotes,
    [property: JsonPropertyName("variantSpellings")] IReadOnlyList<string> VariantSpellings) : IDictionaryEntry
{
    /// <inheritdoc/>
    IEntryPronunciation? IDictionaryEntry.Pronunciation => Pronunciation;

    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntryBlock> IDictionaryEntry.PartOfSpeechBlocks => PartOfSpeechBlocks;

    /// <inheritdoc/>
    IReadOnlyList<IEntrySupplementaryNote> IDictionaryEntry.SupplementaryNotes => SupplementaryNotes;

    /*
    [JsonPropertyName("variantForms")]
    public IReadOnlyList<string> VariantForms { get; }

    [JsonPropertyName("referenceData")]
    public IReadOnlyList<string> ReferenceData { get; }

    [JsonPropertyName("relatedForms")]
    public IReadOnlyList<string> RelatedForms { get; }

    [JsonPropertyName("confusables")]
    public IReadOnlyList<string> Confusables { get; }

    [JsonPropertyName("derivedForms")]
    public IReadOnlyList<string> DerivedForms { get; }

    [JsonPropertyName("contentBlocks")]
    public IReadOnlyList<ContentBlock> ContentBlocks { get; } // used in idioms like gray
    */
}

/*
public class ContentBlock
{
    [JsonPropertyName("title")]
    public object Title { get; }

    [JsonPropertyName("content")]
    public IReadOnlyList<ContentBlockContent> Content { get; }
}

public class ContentBlockContent
{
    [JsonPropertyName("type")]
    public string Type { get; } // list or paragraph

    [JsonConverter(typeof(ArrayOrStringConverter))]
    [JsonPropertyName("content")]
    public IReadOnlyList<string> Content { get; }
}
*/