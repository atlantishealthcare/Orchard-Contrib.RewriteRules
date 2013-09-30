using System;

namespace Contrib.RewriteRules.Exceptions {
    public class RuleEvaluationException : Exception {
        public RuleEvaluationException(string message, string rule, string url) : base(message) {
            Rule = rule;
            Url = url;
        }

        public string Rule { get; set; }
        public string Url { get; set; }
    }
}