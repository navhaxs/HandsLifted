using System;
using System.Linq;
using System.Net;
using HandsLiftedApp.Importer.OnlineSongLyrics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Importer;

[TestClass]
public class SessionCookiePersistenceTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);

    // The WebView2 adapter reports a session cookie as an unset (default) Expires.
    private static Cookie SessionCookie(string name, string domain) =>
        new(name, "v", "/", domain) { HttpOnly = true, Secure = true };

    private static Cookie PersistentCookie(string name, string domain, DateTime expires) =>
        new(name, "v", "/", domain) { Expires = expires };

    [TestMethod]
    public void SessionCookies_GetAnExpiry()
    {
        var a = SessionCookie("auth", "login.example.com");
        var b = SessionCookie("session", ".example.com");

        var selected = SessionCookiePersistence.SelectToPersist(new[] { a, b }, Now);

        Assert.AreEqual(2, selected.Count);
        foreach (var c in selected)
        {
            Assert.AreEqual(Now + SessionCookiePersistence.Lifetime, c.Expires);
        }
    }

    [TestMethod]
    public void SessionCookiesFromAnySite_AreIncluded()
    {
        var one = SessionCookie("sid", "one.test");
        var two = SessionCookie("sid", ".two.test");

        Assert.AreEqual(2, SessionCookiePersistence.SelectToPersist(new[] { one, two }, Now).Count);
    }

    [TestMethod]
    public void PreservesNameValueFlagsAndDomain()
    {
        var jwt = SessionCookie("token", ".example.com");
        jwt.Value = "abc.def.ghi";

        var selected = SessionCookiePersistence.SelectToPersist(new[] { jwt }, Now).Single();

        Assert.AreEqual("token", selected.Name);
        Assert.AreEqual("abc.def.ghi", selected.Value);
        Assert.AreEqual(".example.com", selected.Domain);
        Assert.AreEqual("/", selected.Path);
        Assert.IsTrue(selected.HttpOnly);
        Assert.IsTrue(selected.Secure);
    }

    [TestMethod]
    public void AlreadyPersistentCookies_AreLeftAlone()
    {
        var persistent = PersistentCookie("culture", "login.example.com", Now.AddDays(365));

        var selected = SessionCookiePersistence.SelectToPersist(new[] { persistent }, Now);

        Assert.AreEqual(0, selected.Count);
        Assert.AreEqual(Now.AddDays(365), persistent.Expires);
    }
}
