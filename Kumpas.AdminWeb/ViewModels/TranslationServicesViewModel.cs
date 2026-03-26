namespace Kumpas.AdminWeb.ViewModels;

public class TranslationServicesViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalSessions { get; set; }
    public int TotalMessages { get; set; }
    public int TotalGestureLibraryItems { get; set; }
    public int TotalArModels { get; set; }
    public int TotalRecognitionRecords { get; set; }
    public int TotalErrorLogs { get; set; }
    public decimal AverageMessagesPerSession { get; set; }
    public IReadOnlyList<SystemLogItemViewModel> Logs { get; set; } = [];
}
