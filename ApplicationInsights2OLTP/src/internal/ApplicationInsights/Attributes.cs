using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationInsights
{

    internal static class Attributes
    {
        public const string ParentId = "ParentId";
        public const string OperationId = "OperationId";
        public const string AppRoleName = "AppRoleName";
        public const string AppRoleInstance = "AppRoleInstance";
        public const string DependencyType = "DependencyType";
        public const string Name = "Name";
        public const string Url = "Url";
        public const string Status = "Status";
        public const string OperationName = "OperationName";
        public const string Type = "Type";
        public const string Id = "Id";
        public const string Time = "time";
        public const string Data = "Data";
        public const string Duration = "DurationMs";
        public const string HttpMethod = "HttpMethod";
        public const string HttpPath = "HttpPath";
        public const string ResultCode = "ResultCode";
        public const string Success = "Success";
        public const string SDKVersion = "SDKVersion";
        public const string Target = "Target";
        public const string Source = "Source";

        // AppTraces / AppExceptions
        public const string Message = "Message";
        public const string SeverityLevel = "SeverityLevel";

        // AppExceptions
        public const string ExceptionType = "ExceptionType";
        public const string OuterMessage = "OuterMessage";
        public const string ProblemId = "ProblemId";
        public const string HandledAt = "HandledAt";
        public const string Details = "Details";
        public const string ParsedStack = "parsedStack";
        public const string StackAssembly = "assembly";
        public const string StackMethod = "method";
        public const string StackFileName = "fileName";
        public const string StackLine = "line";
    }
}
