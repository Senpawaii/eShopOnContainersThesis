using Catalog.API.DependencyServices;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Threading;

namespace Catalog.API.Infrastructure.Interceptors {
    public class DemoInterceptor : DbCommandInterceptor {
        public DemoInterceptor(IScopedMetadata svc) {
            _scopedMetadata = svc;
        }

        public IScopedMetadata _scopedMetadata;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

            ModifyCommand(command);

            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) {

            ModifyCommand(command);

            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        private void ModifyCommand(DbCommand command) {
            Console.WriteLine($"Scoped Metadata Tokens: {_scopedMetadata.ScopedMetadataTokens}");
            Console.WriteLine($"Scoped Metadata Interval: {_scopedMetadata.ScopedMetadataInterval.Item1}:{_scopedMetadata.ScopedMetadataInterval.Item2}");
            _scopedMetadata.ScopedMetadataIntervalLow++;
            _scopedMetadata.ScopedMetadataIntervalHigh +=2;

        }
    }
}
