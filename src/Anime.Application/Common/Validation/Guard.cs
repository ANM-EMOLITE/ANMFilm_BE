using System.Globalization;
using Anime.Application.Common.Exceptions;

namespace Anime.Application.Common.Validation;

/// <summary>
/// Helper validate tham số. Ném <see cref="ValidationException"/> khi không hợp lệ.
/// </summary>
public static class Guard
{
    public static string Required(string? value, string name, string code = ErrorCodes.MissingParameter)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{name} is required", code);

        return value.Trim();
    }

    public static int Positive(int value, string name, string code = ErrorCodes.InvalidId)
    {
        if (value <= 0)
            throw new ValidationException($"{name} must be greater than 0", code);

        return value;
    }

    public static int InRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new ValidationException($"{name} must be between {min} and {max}");

        return value;
    }

    public static int NotNegative(int value, string name)
    {
        if (value < 0)
            throw new ValidationException($"{name} cannot be negative");

        return value;
    }

    /// <summary>Kiểm tra giá trị thuộc tập cho phép (không phân biệt hoa thường), trả về dạng UPPER.</summary>
    public static string? OneOf(string? value, string name, IReadOnlyCollection<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();

        if (!allowed.Contains(normalized))
            throw new ValidationException(
                $"Invalid {name}: {value}. Must be one of: {string.Join(", ", allowed)}");

        return normalized;
    }

    /// <summary>Kiểm tra định dạng YYYY-MM-DD.</summary>
    public static DateOnly? Date(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ValidationException($"Invalid {name}: {value}. Expected format YYYY-MM-DD");

        return date;
    }

    public static void DateRange(DateOnly? from, DateOnly? to, string fromName, string toName)
    {
        if (from is not null && to is not null && from > to)
            throw new ValidationException($"{fromName} must be before or equal to {toName}");
    }

    /// <summary>Kiểm tra danh sách số nguyên dương phân cách bằng dấu phẩy, trả về chuỗi đã chuẩn hóa.</summary>
    public static string? IdList(string? value, string name, string code = ErrorCodes.InvalidParameter)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ids = new List<int>(parts.Length);

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new ValidationException(
                    $"Invalid {name}: '{part}'. Must be comma-separated positive integers (e.g. 1,2,3)", code);

            ids.Add(id);
        }

        if (ids.Count == 0)
            throw new ValidationException($"No valid {name} provided", code);

        return string.Join(',', ids.Distinct());
    }
}
