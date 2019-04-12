﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;

namespace QuickWeb.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class RewriteOptionsExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static RewriteOptions AddRedirectToNonWww(this RewriteOptions options)
        {
            options.Rules.Add(new NonWwwRule());
            return options;
        }
    }

    internal class NonWwwRule : IRule
    {
        public void ApplyRule(RewriteContext context)
        {
            var req = context.HttpContext.Request;
            var currentHost = req.Host;
            if (currentHost.Host.Equals("127.0.0.1") || currentHost.Host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Result = RuleResult.ContinueRules;
                return;
            }

            if (Regex.IsMatch(currentHost.Host, @"(\w+\.)(.+\..+)", RegexOptions.Compiled))
            {
                string domain = Regex.Match(currentHost.Host, @"(\w+\.)(.+\..+)").Groups[2].Value;
                var newHost = new HostString(domain);
                var newUrl = new StringBuilder().Append("https://").Append(newHost).Append(req.PathBase).Append(req.Path).Append(req.QueryString);
                context.HttpContext.Response.Redirect(newUrl.ToString());
                context.Result = RuleResult.EndResponse;
            }
        }
    }
}