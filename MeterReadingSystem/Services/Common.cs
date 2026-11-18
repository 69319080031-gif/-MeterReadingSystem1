using System.Globalization;

namespace MeterReadingSystem.Services;

public class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
    public new static ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
}

public class PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Display formatting shared by views, messages and exports (Gregorian dd/MM/yyyy).</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Inv);
    public static string DateTime(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Inv);
    public static string Num(decimal v) => v.ToString("#,##0.###", Inv);
    public static string Num(decimal? v) => v.HasValue ? Num(v.Value) : "-";
    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", Inv);
}
