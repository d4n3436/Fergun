using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Fergun;

internal class CustomContractResolver : DefaultContractResolver
{
    private readonly CustomJsonTextWriter _textWriter;
    private readonly int _maxDepth;

    public CustomContractResolver(CustomJsonTextWriter textWriter, int maxDepth)
    {
        _textWriter = textWriter;
        _maxDepth = maxDepth;
    }

    protected override JsonProperty CreateProperty(
        MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        var shouldSerialize = property.ShouldSerialize;
        property.ShouldSerialize = obj => _textWriter.CurrentDepth <= _maxDepth && (shouldSerialize == null || shouldSerialize(obj));
        return property;
    }
}