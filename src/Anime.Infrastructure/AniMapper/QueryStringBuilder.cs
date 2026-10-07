using System.Globalization;
using System.Text;

namespace Anime.Infrastructure.AniMapper;

/// <summary>
/// Build URL có query string, tự bỏ qua giá trị null/rỗng và escape giá trị.
/// </summary>
internal sealed class QueryStringBuilder(string path)
{
    private readonly List<KeyValuePair<string, string>> _params = [];

    public QueryStringBuilder Add(string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _params.Add(new(key, value));

        return this;
    }

    public QueryStringBuilder Add(string key, int? value)
        => value is null ? this : Add(key, value.Value.ToString(CultureInfo.InvariantCulture));

    public override string ToString()
    {
        if (_params.Count == 0)
            return path;

        var sb = new StringBuilder(path).Append('?');

        for (var i = 0; i < _params.Count; i++)
        {
            if (i > 0)
                sb.Append('&');

            sb.Append(Uri.EscapeDataString(_params[i].Key))
              .Append('=')
              .Append(Uri.EscapeDataString(_params[i].Value));
        }

        return sb.ToString();
    }
}
