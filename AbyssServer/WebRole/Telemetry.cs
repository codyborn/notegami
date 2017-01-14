using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.ApplicationInsights;
using System.Diagnostics;

namespace WebRole
{
    public class RequestTracker : IDisposable
    {
        private static TelemetryClient telemetry = new TelemetryClient();
        private Stopwatch _stopwatch;
        private string _requestName;
        public RequestResponse response = RequestResponse.Success;
        
        public RequestTracker(string requestName, string id)
        {
            _requestName = requestName;
            // Operation Id and Name are attached to all telemetry and help you identify
            // telemetry associated with one request:
            telemetry.Context.Operation.Id = id.ToLowerInvariant();
            telemetry.Context.Operation.Name = _requestName;

            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            telemetry.TrackRequest(_requestName, DateTime.Now,
               _stopwatch.Elapsed,
               response.ToString(), true);  // Response code, success
        }

        public enum RequestResponse
        {
            Success = 200,
            UserError,
            ServerError,
            LoginOnSignup
        }
    }

    public static class ExceptionTracker
    {
        private static TelemetryClient telemetry = new TelemetryClient();
        public static void LogException(Exception e)
        {
            telemetry.TrackException(e);
        }
    }
}