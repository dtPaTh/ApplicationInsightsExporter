using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationInsights
{
    internal static class SemanticConventions
    {
        public const string InstrumentationLibraryName = "ApplicationInsights";

        public const string ScopeAppInsights = "appinsights";
        public const string ScopeProperties = "prop";

        public const string DependencyType = ScopeAppInsights+".dependencytype";
        public const string Name = ScopeAppInsights + ".name";
        public const string Url = ScopeAppInsights + ".url";
        public const string Status = ScopeAppInsights + ".status";
        public const string Data = ScopeAppInsights + ".data";
        public const string ResultCode = ScopeAppInsights + ".resultcode";

        

    }
}
