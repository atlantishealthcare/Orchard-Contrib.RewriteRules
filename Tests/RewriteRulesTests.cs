using System;
using System.Web;
using System.Web.Mvc;
using Contrib.RewriteRules.Exceptions;
using Contrib.RewriteRules.Services;
using Moq;
using NUnit.Framework;
using Orchard.Services;

namespace Contrib.RewriteRules.Tests {

    // http://httpd.apache.org/docs/2.0/mod/mod_rewrite.html
    // http://httpd.apache.org/docs/2.0/rewrite/rewrite_guide.html

    [TestFixture]
    public class RewriteRulesTests {
        protected RulesInterpreter _rulesInterpreter;
        protected Mock<HttpContextBase> _context;

        protected string _rewriteBase = ""; // as per http://httpd.apache.org/docs/current/mod/mod_rewrite.html#rewritebase

        [SetUp]
        public void Setup() {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            _rulesInterpreter = new RulesInterpreter(clock.Object);
            _context = new Mock<HttpContextBase>();
            SetRewriteBase("");
        }

        private void SetRewriteBase(string applicationPath) {
            _rewriteBase = applicationPath;
        }

        private ActionResult Execute(string url, string rule) {
            _context.SetupGet(c => c.Request.Url).Returns(new Uri(url));
            _context.SetupGet(c => c.Request.ApplicationPath).Returns(_rewriteBase);

            var cookies = new HttpCookieCollection();
            _context.SetupGet(c => c.Response.Cookies).Returns(cookies);
            _context.Setup(c => c.Response.SetCookie(It.IsAny<HttpCookie>())).Callback<HttpCookie>(cookies.Set);

            return _rulesInterpreter.Interpret(_context.Object, rule);
        }

        [Test]
        public void RulesShouldIgnoreComments() {
            const string rules1 = @"
                # This is a comment
            ";

            const string rules2 = @"
                # This is a 
                # multiline comment
            ";

            const string rules3 = @"
                # This is a 
                # multiline comment
                RewriteRule foo bar
            ";

            Assert.That(Execute("http://www.foo.org/foo", rules1), Is.Null);
            Assert.That(Execute("http://www.foo.org/foo", rules2), Is.Null);
            Assert.That(Execute("http://www.foo.org/foo", rules3), Is.Not.Null);
        }

        [Test]
        public void ShouldRewriteHomepage() {
            var result = Execute("http://www.foo.org/", @"RewriteRule ^/$ /homepage.html") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.html"));
        }

        [Test]
        public void ShouldIngorePorts() {
            var result = Execute("http://www.foo.org:42/", @"RewriteRule ^/$ /homepage.html") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.html"));
        }

        [Test]
        public void ShouldHandleApplicationPath() {
            SetRewriteBase("/orchard");

            var result = Execute("http://www.foo.org/orchard/", @"RewriteRule ^/$ /homepage.html") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.html"));
        }

        [Test]
        public void ShouldNotTransformAbsoluteUrls() {
            var result1 = Execute("http://www.foo.org/", @"RewriteRule ^/$ http://www.microsoft.com") as TransferResult;

            Assert.That(result1, Is.Not.Null);
            Assert.That(result1.Url, Is.EqualTo("http://www.microsoft.com"));
        }

        [Test]
        public void ShouldNotTransformAbsoluteUrlsWhenApplicationPathIsSet() {
            SetRewriteBase("/orchard");
            var result1 = Execute("http://www.foo.org/orchard", @"RewriteRule ^/$ http://www.microsoft.com") as TransferResult;

            Assert.That(result1, Is.Not.Null);
            Assert.That(result1.Url, Is.EqualTo("http://www.microsoft.com"));
        }

        [Test]
        public void HandleNegatedRewriteRules() {
            var result = Execute("http://www.foo.org/", @"RewriteRule !^/$ /homepage.html") as TransferResult;
            Assert.That(result, Is.Null);
        }

        [Test]
        public void BackReferencesShouldBeApplied() {
            var result = Execute(@"http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));
        }

        [Test]
        public void ShouldHandleFlagNoCase() {
            var result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php") as TransferResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));

            result = Execute("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php") as TransferResult;
            Assert.That(result, Is.Null);

            result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [NC]") as TransferResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));

            result = Execute("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [NC]") as TransferResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));

            result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [nocase]") as TransferResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));

            result = Execute("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [nocase]") as TransferResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.php"));
        }

        [Test]
        public void ShouldHandleFlagRedirect() {
            var result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [R]");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(RedirectResult)));
            Assert.That(((RedirectResult)result).Permanent, Is.False);

            result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [redirect]");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(RedirectResult)));
            Assert.That(((RedirectResult)result).Permanent, Is.False);

            result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [R=302]");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(RedirectResult)));
            Assert.That(((RedirectResult)result).Permanent, Is.False);

            result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule (.*)\.aspx $1.php [R=301]");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(RedirectResult)));
            Assert.That(((RedirectResult)result).Permanent, Is.True);
        }

        [Test]
        public void ShouldHandleMultipleFlags() {
            var result = Execute("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [R]");
            Assert.That(result, Is.Null);

            result = Execute("http://www.foo.org/homepage.ASPX", @"RewriteRule (.*)\.aspx $1.php [R, NC]");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(RedirectResult)));
            Assert.That(((RedirectResult)result).Permanent, Is.False);
        }

        [Test]
        public void ShouldHandleBackReferences() {
            var result = Execute(
                            @"http://www.foo.org/blog/2003-nov",
                            @"RewriteRule ^/blog/([0-9]+)-([a-z]+) http://foo.org/blog/index.php?archive=$1-$2 [NC]"
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(TransferResult)));
            Assert.That(((TransferResult)result).Url, Is.EqualTo(@"http://foo.org/blog/index.php?archive=2003-nov"));
        }

        [Test]
        public void ShouldHandleFlagChain() {
            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) $1/b [C]
                              RewriteRule (a/b) $1/c [C]
                              RewriteRule (a/b/c) $1/d
                            "
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(TransferResult)));
            Assert.That(((TransferResult)result).Url, Is.EqualTo(@"a/b/c/d"));

            result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (b) $1/b [C]
                              RewriteRule (a) $1/c [C]
                              RewriteRule (a) $1/d
                            "
                                    );

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(TransferResult)));
            Assert.That(((TransferResult)result).Url, Is.EqualTo(@"a/d"));
        }


        [Test]
        public void ShouldHandleNoSubstitution() {
            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) -"
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(((TransferResult)result).Url, Is.EqualTo(@"/a"));
        }

        [Test]
        public void ShouldHandleFlagCookie() {
            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) $1/b [CO=foo:bar:foo.org]"
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(_context.Object.Response.Cookies["foo"].Value, Is.EqualTo("bar"));

            result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) - [CO=foo:bar:foo.org]
                              RewriteRule (a) - [CO=bar:baz:foo.org:0:mypath]"
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(_context.Object.Response.Cookies["foo"].Value, Is.EqualTo("bar"));
            Assert.That(_context.Object.Response.Cookies["bar"].Value, Is.EqualTo("baz"));
            Assert.That(_context.Object.Response.Cookies["bar"].Path, Is.EqualTo("mypath"));
        }

        [Test]
        public void ShouldHandleFlagEnv() {

            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) - [E=foo:bar]
                              RewriteRule (a) %{foo}"
            );

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult) result).Url, Is.EqualTo("bar"));
        }

        [Test]
        public void FlagEnvShouldExpandBackReferences() {
            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) - [E=foo:$1]
                                RewriteRule (a) %{foo}"
            );

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("a"));
        }

        [Test, ExpectedException(typeof(RuleEvaluationException), ExpectedMessage = "Environment variable not found: %{foo}")]
        public void FlagEnvShouldBeReseted() {
            var result = Execute(
                            @"http://www.foo.org/a",
                            @"RewriteRule (a) - [E=foo:$1]
                                RewriteRule (a) %{foo}
                                RewriteRule (a) - [E=!foo]
                                RewriteRule (a) %{foo}"
            );
        }

        [Test]
        public void ShouldHandleConditionsBackReferences() {

            var result = Execute(
                            @"http://www.foo.org/a/b/c",
                            @"RewriteCond a/b/c (.*)/(.*)/(.*)
                              RewriteRule ^/(.*)$ %3/%2/%1"
                        );

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("c/b/a"));
        }

        [Test]
        public void ShouldHandleFlagForbidden() {
            var result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule ^/(.*)\.php$ - [F]");
            Assert.That(result, Is.Null);

            // ensure the execution is stopped when F is processed
            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ - [F]
                              RewriteRule ^/(.*)$ a
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(HttpUnauthorizedResult)));

            // ensure the execution is stopped when F is processed
            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ - [forbidden]
                              RewriteRule ^/(.*)$ a
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(HttpUnauthorizedResult)));
        }

        [Test]
        public void ShouldHandleFlagGone() {
            var result = Execute("http://www.foo.org/homepage.aspx", @"RewriteRule ^/(.*)\.php$ - [G]");
            Assert.That(result, Is.Null);

            // ensure the execution is stopped when G is processed
            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ - [G]
                              RewriteRule ^/(.*)$ a
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(HttpStatusCodeResult)));
            Assert.That(((HttpStatusCodeResult)result).StatusCode, Is.EqualTo(410));

            // ensure the execution is stopped when G is processed
            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ - [gone]
                              RewriteRule ^/(.*)$ a
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(typeof(HttpStatusCodeResult)));
            Assert.That(((HttpStatusCodeResult)result).StatusCode, Is.EqualTo(410));
        }

        [Test]
        public void ShouldHandleFlagLast() {
            var result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a 
                              RewriteRule ^/(.*)$ b [L]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("b"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a 
                              RewriteRule ^/(.*)$ b [last]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("b"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/blah$ a [L]
                              RewriteRule ^/(.*)$ b [L]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("b"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/blah$ a [L]
                              RewriteRule ^/blah$ b 
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("c"));
        }

        [Test]
        public void ShouldHandleFlagNext() {
            var result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ $0a
                              RewriteRule !^/homepage.aspxaaaa$ - [N]
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("/homepage.aspxaaaa"));
        }

        [Test]
        public void ShouldHandlerFlagQSA() {
            // Simply use a question mark inside the substitution string to indicate that the following text should be re-injected into the query string. When you want to erase an existing query string, end the substitution string with just a question mark. To combine new and old query strings, use the [QSA] flag.

            var result = Execute("http://domain.com/grab/foobar.zip?level=5&foo=bar",
                            @"RewriteCond %{QUERY_STRING} foo=(.+)
                              RewriteRule ^/grab/(.*) /%1/index.php?file=$1 [QSA]
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("/bar/index.php?file=foobar.zip&level=5&foo=bar"));
        }

        [Test]
        public void ShouldNotAlterQueryString() {
            // By default, the query string is passed through unchanged. 

            var result = Execute("http://www.foo.org?foo=foo&bar=bar", @"RewriteRule ^/$ /homepage.html") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.html?foo=foo&bar=bar"));
        }

        [Test]
        public void ShouldSubsituteQueryString() {
            // You can, however, create URLs in the substitution string containing a query string part. 

            var result = Execute("http://www.foo.org?foo=foo&bar=bar", @"RewriteRule ^/$ /homepage.html?blah") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("/homepage.html?blah"));
        }        

        [Test]
        public void ShouldHandleFlagSkip() {
            var result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a [S=1]
                              RewriteRule ^/(.*)$ b [L]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("c"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/foo$ a [S=1]
                              RewriteRule ^/(.*)$ b [L]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("b"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a [S=2]
                              RewriteRule ^/(.*)$ b [L]
                              RewriteRule ^/(.*)$ c
                            ");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("a"));
        }

        [Test]
        public void ShouldHandleFlagType() {
            _context.SetupSet(c => c.Request.ContentType).Callback(ct => Assert.That(ct, Is.EqualTo("application/x-foo")));

            var result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a [T=application/x-foo]");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("a"));

            result = Execute("http://www.foo.org/homepage.aspx",
                            @"RewriteRule ^/(.*)$ a [T=application/x-foo]");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("a"));
        }

        [Test]
        public void HttpHostShouldBeUsableInRewriteConditions() {
            _rulesInterpreter.PopulateEnvironmentVariables = (c, e) => {
                e["HTTP_HOST"] = () => "new.dotnetdave.net";
            };

            var result = Execute("http://new.dotnetdave.net/2010/04/22/upgraded-to-visual-studio-2010/",
                            @"RewriteCond %{HTTP_HOST} ^new.dotnetdave.net$ [NC]
                              RewriteRule ^/[0-9]*/[0-9]*/[0-9]*/([^/]*)/?$ /blog/$1 [R=301,L]");

            Assert.That(result, Is.Not.Null);
            Assert.That(((RedirectResult)result).Url, Is.EqualTo("/blog/upgraded-to-visual-studio-2010"));
        }

        [Test]
        public void ShouldRemoveTrailingSlash() {
            var result = Execute(@"http://www.foo.org/about/", @"RewriteRule ^/about/$ about") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("about"));
        }

        [Test]
        public void ShouldRemoveTrailingSlash2() {
            var result = Execute(@"http://www.foo.org/about/", @"RewriteRule ^/(.*)/$ $1") as TransferResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("about"));
        }
    }
}