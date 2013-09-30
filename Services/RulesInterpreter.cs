using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Contrib.RewriteRules.Exceptions;
using Orchard.Services;

namespace Contrib.RewriteRules.Services {
    public class RulesInterpreter {
        private readonly IClock _clock;

        public RulesInterpreter(IClock clock) {
            _clock = clock;
        }

        private static readonly Regex _conditionBackReferenceRegex = new Regex(@"\%(?<index>\d)", RegexOptions.Compiled);
        private static readonly Regex _ruleBackReferenceRegex = new Regex(@"\$(?<index>\d)", RegexOptions.Compiled);
        private static readonly Regex _environmentVariableRegex = new Regex(@"\%{(?<variable>[^}]*)}", RegexOptions.Compiled);
        
        public Action<HttpContextBase, Dictionary<string, Func<string>>> PopulateEnvironmentVariables { get; set; }

        public ActionResult Interpret(HttpContextBase context, string rules) {
            var sr = new StringReader(rules ?? "");
            var rewriteCond = true;
            var previousRuleMatched = true;
            var toSkip = 0;
            GroupCollection lastConditionGroups = null;

            ActionResult result = null;

            if(context == null) {
                return result;
            }

            var request = context.Request;
            string url = request.Url.PathAndQuery; // contains a leading /

            var environmentVariables = new Dictionary<string, Func<string>>();
            
            populateEnvironmentVariables(context, environmentVariables);

            if(PopulateEnvironmentVariables != null) {
                PopulateEnvironmentVariables(context, environmentVariables);
            }

            environmentVariables.Add("QUERY_STRING", () => ExtractQueryString(url));

            string line;
            while (null != (line = sr.ReadLine())) {
                line = line.Trim();

                // ignore empty lines
                if(String.IsNullOrEmpty(line)) {
                    continue;
                }

                // comments
                if (line.StartsWith("#")) {
                    continue;
                }

                // rewrite conditions
                // http://httpd.apache.org/docs/2.0/mod/mod_rewrite.html#RewriteCond
                if (line.StartsWith("RewriteCond")) {

                    var parts = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    var pattern = parts[1];
                    var value = parts[2];
                    var flags = parts.Length > 3
                                    ? parts[3].TrimStart('[').TrimEnd(']').Split(new[] {',', ' '},
                                                                                 StringSplitOptions.RemoveEmptyEntries)
                                    : new string[0];

                    var noCase = flags.Any(f => f == "nocase" || f == "NC");
                    var orNext = flags.Any(f => f == "ornext" || f == "OR");

                    // don't evaluate condition if the previous one was false
                    if (!orNext && !rewriteCond) {
                        continue;
                    }

                    // applying server variable substitution in the result
                    pattern = ExpandEnvironmentVariables(environmentVariables, pattern, url, line);

                    var match = TestCondition(pattern, value, noCase, out lastConditionGroups);

                    rewriteCond = orNext ? rewriteCond || match : rewriteCond && match;
                }

                // rewrite rules
                if (line.StartsWith("RewriteRule")) {
                    // skiping line is defined by a previous flag
                    if(toSkip > 0) {
                        toSkip--;
                        continue;
                    }

                    // previous condition is true ?
                    if (!rewriteCond) {
                        rewriteCond = true;
                        continue;
                    }

                    var parts = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    var from = parts[1];
                    var to = parts[2];
                    var flags = parts.Length > 3 ? GetFlags(line) : new string[0];

                    var chain = flags.Any(f => f == "C" || f == "chain");
                    var cookie = flags.Any(f => f.StartsWith("CO=") || f.StartsWith("cookie="));
                    var env = flags.Any(f => f.StartsWith("E=") || f.StartsWith("env="));
                    var noCase = flags.Any(f => f == "NC" || f == "nocase");
                    var lastRule = flags.Any(f => f == "L" || f == "last");
                    var redirect = flags.Any(f => f == "R" || f == "redirect" || f.StartsWith("R="));
                    var permanent = redirect && flags.Any(f => f == "R=301" || f == "redirect=301");
                    var forbidden = flags.Any(f => f == "F" || f == "forbidden");
                    var gone = flags.Any(f => f == "G" || f == "gone");
                    var next = flags.Any(f => f == "N" || f == "next");
                    var qsa = flags.Any(f => f == "QSA" || f == "qsappend");
                    var skip = flags.Any(f => f.StartsWith("S=") || f.StartsWith("skip="));
                    var type = flags.Any(f => f.StartsWith("T=") || f.StartsWith("type="));
                    var negate = false;

                    // ignore this rule if 'chain' is set and previous rule didn't match
                    if(!chain || previousRuleMatched) {
                        try {
                            // get the path part of the url, even if in a virtual folder
                            var path = url.Substring(request.ApplicationPath.Length);
                            
                            // removing the query string from the rewrite pattern
                            if (path.Contains('?')) {
                                path = path.Substring(0, path.IndexOf('?'));
                            }

                            // in case ApplicationPath is not null, forces the leading /
                            if(!path.StartsWith("/")) {
                                path = "/" + path;
                            }

                            // storing the query string for QSA flag
                            var queryString = ExtractQueryString(url);

                            // negated pattern
                            if (from.StartsWith("!")) {
                                from = from.Substring(1);
                                negate = true;
                            }

                            var options = noCase ? RegexOptions.IgnoreCase : RegexOptions.None;

                            var match = Regex.Match(path, from, options);
                            
                            if (match.Success != negate) {
                                if(to != "-") {
                                    
                                    // applying server variable substitution in the result
                                    to = ExpandEnvironmentVariables(environmentVariables, to, url, line);

                                    // applying condition back references substitution in the result
                                    to = ExpandConditionBackReferences(to, lastConditionGroups, url, line);

                                    // applying rules back references substitution in the result
                                    to = ExpandRuleBackReference(match, to, url, line);

                                    url = to;

                                    if(!String.IsNullOrEmpty(queryString)) {
                                        // if [qsa], append the query strings
                                        if(qsa) {
                                            if (url.Contains('?')) {
                                                url += "&" + queryString;
                                            }
                                            else {
                                                url += "?" + queryString; // queryString has the ? stipped when extracted from original url
                                            }
                                        }
                                        else {
                                            // if the new url doesn't contain a ?, preserve original querystring
                                            if (!url.Contains('?')) {
                                                url += "?" + queryString;
                                            }
                                        }
                                    }
                                }
                                else {
                                    url = path;
                                }

                                if (forbidden) {
                                    return new HttpUnauthorizedResult();
                                }
                                
                                if (gone) {
                                    return new HttpStatusCodeResult(410, "Gone");
                                }
                                
                                if(cookie) {
                                    CreateCookies(context, flags);
                                }

                                if (env) {
                                    foreach (var flag in flags.Where(f => f.StartsWith("E=") || f.StartsWith("env="))) {
                                        var value = flag.Substring(flag.IndexOf('=') + 1);

                                        // env|E=VAR:VAL
                                        var values = value.Split(':');

                                        // env|E=!VAR
                                        if (values.Length == 1) {
                                            if (values[0].StartsWith("!")) {
                                                environmentVariables.Remove(values[0].Substring(1));
                                            }
                                            else {
                                                throw new RuleEvaluationException("Invalid arguments in env definition: " + flag, line, url);
                                            }
                                        }

                                        environmentVariables[values[0]] =
                                            () => {
                                                var variable = ExpandConditionBackReferences(values[1], lastConditionGroups, url, line);
                                                variable = ExpandRuleBackReference(match, variable, url, line);
                                                return variable;
                                            };
                                    }
                                }

                                if(next) {
                                    sr = new StringReader(rules ?? "");
                                }

                                if (skip) {
                                    var flag = flags.Where(f => f.StartsWith("S=") || f.StartsWith("skip=")).First();
                                    toSkip = Int32.Parse(flag.Substring(flag.IndexOf('=') + 1));
                                }

                                if (type) {
                                    var flag = flags.Where(f => f.StartsWith("T=") || f.StartsWith("type=")).First();
                                    var mime = flag.Substring(flag.IndexOf('=') + 1);
                                    context.Response.ContentType = mime;
                                }

                                result = redirect ? new RedirectResult(url, permanent) : new TransferResult(url);

                                previousRuleMatched = true;

                                // stop precessing rules ?
                                if (lastRule) {
                                    return result;
                                }
                            }
                            else {
                                previousRuleMatched = false;
                            }

                            
                        }
                        catch (Exception ex) {
                            throw new RuleEvaluationException(ex.Message, "", line);
                        }
                    }
                }
            }

            return result;
        }

        private static string ExpandEnvironmentVariables(Dictionary<string, Func<string>> environmentVariables, string to, string url, string rule) {
            // prevent a regular expression evaluation if not necessary
            if (!to.Contains("%{")) {
                return to;
            }

            to = _environmentVariableRegex.Replace(to,
                               m => {
                                   var variable = m.Groups["variable"].Value;
                                   if (!environmentVariables.ContainsKey(variable)) {
                                       throw new RuleEvaluationException("Environment variable not found: %{" + variable + "}", rule, url);
                                   }

                                   return environmentVariables[variable]();
                               });
            return to;
        }

        private static string ExpandRuleBackReference(Match match, string to, string url, string rule) {
            // prevent a regular expression evaluation if not necessary
            if (!to.Contains("$")) {
                return to;
            }

            to = _ruleBackReferenceRegex.Replace(to,
                               m => {
                                   var index = Int32.Parse(m.Groups["index"].Value);
                                   if (match.Groups.Count < index) {
                                       throw new RuleEvaluationException("Rule back reference index not found: $" + index , rule, url);
                                   }

                                   return match.Groups[index].Value;
                               });
            return to;
        }

        private static string ExpandConditionBackReferences(string to, GroupCollection lastConditionGroups, string url, string rule) {
            // prevent a regular expression evaluation if not necessary
            if(!to.Contains("%")) {
                return to;
            }

            to = _conditionBackReferenceRegex.Replace(to,
                               m => {
                                   var index = Int32.Parse(m.Groups["index"].Value);
                                   if (lastConditionGroups == null || lastConditionGroups.Count < index) {
                                       throw new RuleEvaluationException("Condition back reference index not found: %" + index, rule, url);
                                   }

                                   return lastConditionGroups[index].Value;
                               });
            return to;
        }

        private void populateEnvironmentVariables(HttpContextBase context, Dictionary<string, Func<string>> environmentVariables) {
            // HTTP headers
            environmentVariables.Add("HTTP_USER_AGENT", () => context.Request.UserAgent ?? "");
            environmentVariables.Add("HTTP_REFERER", () => context.Request.UrlReferrer == null ? "" : context.Request.UrlReferrer.ToString());
            environmentVariables.Add("HTTP_COOKIE", () => context.Request.Headers["Cookie"] ?? "");
            environmentVariables.Add("HTTP_FORWARDED", () => "");
            environmentVariables.Add("HTTP_HOST", () => context.Request.Headers["Host"]);
            environmentVariables.Add("HTTP_PROXY_CONNECTION", () => "");
            environmentVariables.Add("HTTP_ACCEPT", () => "");

            // connection and request
            environmentVariables.Add("REMOTE_ADDR", () => context.Request.UserHostAddress ?? "");
            environmentVariables.Add("REMOTE_HOST", () => context.Request.UserHostName ?? "");
            environmentVariables.Add("REMOTE_PORT", () => "");
            environmentVariables.Add("REMOTE_USER", () => context.Request.LogonUserIdentity == null ? "" : context.Request.LogonUserIdentity.Name);
            environmentVariables.Add("REMOTE_IDENT", () => "");
            environmentVariables.Add("REQUEST_METHOD", () => context.Request.HttpMethod);
            environmentVariables.Add("SCRIPT_FILENAME", () => "");
            environmentVariables.Add("PATH_INFO", () => "");
            environmentVariables.Add("AUTH_TYPE", () => "");

            // system
            environmentVariables.Add("TIME_YEAR", () => _clock.UtcNow.Year.ToString());
            environmentVariables.Add("TIME_MON", () => _clock.UtcNow.Month.ToString());
            environmentVariables.Add("TIME_DAY", () => _clock.UtcNow.Day.ToString());
            environmentVariables.Add("TIME_HOUR", () => _clock.UtcNow.Hour.ToString());
            environmentVariables.Add("TIME_MIN", () => _clock.UtcNow.Minute.ToString());
            environmentVariables.Add("TIME_SEC", () => _clock.UtcNow.Second.ToString());
            environmentVariables.Add("TIME_WDAY", () => _clock.UtcNow.DayOfWeek.ToString());
            environmentVariables.Add("TIME", () => _clock.UtcNow.TimeOfDay.ToString());

            // specials
            environmentVariables.Add("HTTPS", () => context.Request.IsSecureConnection ? "on" : "off");
            environmentVariables.Add("REQUEST_URI", () => context.Request.Url.PathAndQuery);
        }

        private static void CreateCookies(HttpContextBase context, IEnumerable<string> flags) {
            foreach(var flag in flags.Where(f => f.StartsWith("CO=") || f.StartsWith("cookie="))) {
                var value = flag.Substring(flag.IndexOf('=') + 1);
                                        
                // cookie|CO=NAME:VAL:domain[:lifetime[:path]]
                var values = value.Split(':');

                if(values.Length < 3) {
                    throw new ArgumentException("Invalid arguments in cookie definition: " + flag);
                }

                var c = new HttpCookie(values[0], values[1]) {Domain = values[2]};
                if (values.Length > 3) {
                    // liftime is ignored as we can't get the remote timezone
                } 
                                        
                if (values.Length > 4) {
                    c.Path = values[4];
                }

                context.Response.SetCookie(c);
            }
        }

        private static string[] GetFlags(string line) {
            return line.Trim().Substring(line.LastIndexOf('['))
                .TrimStart('[').TrimEnd(']')
                .Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool TestCondition(string variable, string condition, bool noCase, out GroupCollection groupCollection) {
            groupCollection = null;

            // negate condition ?
            var negate = false;
            if (condition.StartsWith("!")) {
                negate = true;
                condition = condition.Substring(1);
            }

            if (condition.StartsWith("=")) {
                condition = condition.Substring(1);
                return negate ^
                       String.Equals(variable, condition,
                                     noCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }

            if (condition.StartsWith("<")) {
                condition = condition.Substring(1);
                return negate ^ String.CompareOrdinal(variable, condition) == -1;
            }

            if (condition.StartsWith(">")) {
                condition = condition.Substring(1);
                return negate ^ String.CompareOrdinal(variable, condition) == 1;
            }

            // otherwise it's a regular expression
            var match = Regex.Match(variable, condition, noCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            groupCollection = match.Groups;
            return negate ^ match.Success;
        }

        private static string MakeAbsolute(HttpRequestBase request, string url) {
            if (url.StartsWith("~/")) {
                url = url.Substring(2);
                var appPath = request.ApplicationPath;
                if (appPath == "/")
                    appPath = "";
                url = string.Format("{0}/{1}", appPath, url);
            }

            return url;
        }

        private string ExtractQueryString(string url) {
            return url.Contains('?') ? url.Substring(url.IndexOf('?') + 1) : "";
        }
    }
}