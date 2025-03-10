using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;

namespace ERPAPI.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly AppDbContext _appDbContext;

        public LoggerService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public void LogEvent(string message, string category, int triggeredBy, string oldValue = null, string newValue = null)
        {
            var log = new EventLog
            {
                Event = message,
                EventTriggeredBy = triggeredBy,
                Category = category,
                OldValue = oldValue,  // Log the old value if available
                NewValue = newValue   // Log the new value if available
            };
            _appDbContext.EventLogs.Add(log);
            _appDbContext.SaveChanges();
        }

        public void LogEventWithTransaction(string message, string category, int triggeredBy, int transactionId, string oldValue = null, string newValue = null)
        {
            var log = new EventLog
            {
                Event = message,
                EventTriggeredBy = triggeredBy,
                Category = category,
                TransactionId = transactionId,
                OldValue = oldValue,
                NewValue = newValue
            };
            _appDbContext.EventLogs.Add(log);
            _appDbContext.SaveChanges();
        }

        public void LogError(string error, string errormessage, string controller)
        {
            var log = new ErrorLog
            {
                Error = error,
                Message = errormessage,
                OccuranceSpace = controller,
            };

            _appDbContext.ErrorLogs.Add(log);
            _appDbContext.SaveChanges();
        }
    }
}
