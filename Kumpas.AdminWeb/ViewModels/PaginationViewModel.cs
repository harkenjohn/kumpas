namespace Kumpas.AdminWeb.ViewModels;

public class PaginationViewModel
{
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public string ItemLabel { get; set; } = "records";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public IDictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int StartItem => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int EndItem => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);

    public IReadOnlyList<int> VisiblePages
    {
        get
        {
            const int windowSize = 5;
            var start = Math.Max(1, PageNumber - 2);
            var end = Math.Min(TotalPages, start + windowSize - 1);

            if ((end - start + 1) < windowSize)
            {
                start = Math.Max(1, end - windowSize + 1);
            }

            return Enumerable.Range(start, end - start + 1).ToList();
        }
    }

    public IDictionary<string, string> BuildRouteValues(int targetPage)
    {
        var values = new Dictionary<string, string>(RouteValues, StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = targetPage.ToString()
        };

        return values;
    }

    public string SummaryText =>
        TotalCount == 0
            ? $"Showing 0 of 0 {ItemLabel}"
            : $"Showing {StartItem}-{EndItem} of {TotalCount} {ItemLabel}";
}
