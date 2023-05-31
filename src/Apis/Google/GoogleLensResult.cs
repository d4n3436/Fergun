namespace Fergun.Apis.Google;

/// <inheritdoc cref="IGoogleLensResult"/>
public record GoogleLensResult(string Title, string SourcePageUrl, string ThumbnailUrl, string SourceDomainName, string SourceIconUrl) : IGoogleLensResult;