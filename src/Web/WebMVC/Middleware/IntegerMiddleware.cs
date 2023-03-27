using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace WebMVC.Middleware {
    public class IntegerMiddleware {
        private readonly ILogger<IntegerMiddleware> _logger;
        private readonly RequestDelegate _next;
        private static ConcurrentBag<double> _testInts = new ConcurrentBag<double>();

        public IntegerMiddleware(ILogger<IntegerMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        public async Task Invoke(HttpContext ctx) {
            var start = DateTime.UtcNow;
            Task task = _next.Invoke(ctx);
            await task;
            _testInts.Add((DateTime.UtcNow - start).TotalMilliseconds);
            //for (int i = 0; i < _testInts.Count; i++) {
            //    _logger.LogInformation($"Registered double #{i}: {_testInts.ElementAt(i)}");
            //}
        }
    }

    public static class IntegerExtension {
        public static IApplicationBuilder UseInteger(this IApplicationBuilder app) {
            return app.UseMiddleware<IntegerMiddleware>(); 
        }
    }
}
