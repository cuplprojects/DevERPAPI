namespace ERPAPI.Service
{
    public interface ILoggerService
    {
        void LogEvent(string message, string category, int triggeredBy, string oldValue = null, string newValue = null);
        void LogEventWithTransaction(string message, string category, int triggeredBy, int transactionId, string oldValue = null, string newValue = null); // New method
        void LogError(string error, string errorMsg, string controller);
    }
}
