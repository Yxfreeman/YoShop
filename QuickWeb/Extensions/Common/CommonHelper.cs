﻿using Masuit.Tools;
using Masuit.Tools.Html;
using Masuit.Tools.Media;
using Masuit.Tools.Security;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using ContentDispositionHeaderValue = System.Net.Http.Headers.ContentDispositionHeaderValue;

namespace QuickWeb.Extensions.Common
{
    /// <summary>
    /// 公共类库
    /// </summary>
    public static class CommonHelper
    {
        static CommonHelper()
        {
            BanRegex = File.ReadAllText(Path.Combine(AppContext.BaseDirectory + "App_Data", "ban.txt"));
            ModRegex = File.ReadAllText(Path.Combine(AppContext.BaseDirectory + "App_Data", "mod.txt"));
            DenyIP = File.ReadAllText(Path.Combine(AppContext.BaseDirectory + "App_Data", "denyip.txt"));
            DenyAreaIP = JsonConvert.DeserializeObject<ConcurrentDictionary<string, HashSet<string>>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory + "App_Data", "denyareaip.txt")));
            string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory + "App_Data", "DenyIPRange.txt"));
            DenyIPRange = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                try
                {
                    var strs = line.Split(' ');
                    DenyIPRange[strs[0]] = strs[1];
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            IPWhiteList = File.ReadAllText(Path.Combine(AppContext.BaseDirectory + "App_Data", "whitelist.txt")).Split(',', '，').ToList();
        }

        /// <summary>
        /// 敏感词
        /// </summary>
        public static string BanRegex { get; set; }

        /// <summary>
        /// 审核词
        /// </summary>
        public static string ModRegex { get; set; }

        /// <summary>
        /// 全局禁止IP
        /// </summary>
        public static string DenyIP { get; set; }

        /// <summary>
        /// 按地区禁用ip
        /// </summary>
        public static ConcurrentDictionary<string, HashSet<string>> DenyAreaIP { get; set; }

        /// <summary>
        /// ip白名单
        /// </summary>
        public static List<string> IPWhiteList { get; set; }

        /// <summary>
        /// IP黑名单地址段
        /// </summary>
        public static Dictionary<string, string> DenyIPRange { get; set; }

        /// <summary>
        /// 判断IP地址是否被黑名单
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsDenyIpAddress(this string ip)
        {
            if (IPWhiteList.Contains(ip))
            {
                return false;
            }

            return DenyAreaIP.SelectMany(x => x.Value).Union(DenyIP.Split(',')).Contains(ip) || DenyIPRange.Any(kv => kv.Key.StartsWith(ip.Split('.')[0]) && ip.IpAddressInRange(kv.Key, kv.Value));
        }

        /// <summary>
        /// 每IP错误的次数统计
        /// </summary>
        public static ConcurrentDictionary<string, int> IPErrorTimes { get; set; } = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// 系统设定
        /// </summary>
        // TODO: 初始化系统设置参数(未实现) CommonHelper.SystemSettings = db.SystemSetting.ToDictionary(s => s.Name, s => s.Value); - 朱锦润
        public static Dictionary<string, string> SystemSettings { get; set; }

        /// <summary>
        /// 访问量
        /// </summary>
        public static double InterviewCount
        {
            get
            {
                try
                {
                    return RedisHelper.Get<double>("Interview:ViewCount");
                }
                catch
                {
                    return 1;
                }
            }
            set
            {
                try
                {
                    value = RedisHelper.IncrBy("Interview:ViewCount");
                }
                catch
                {
                    value = 0;
                }
            }
        }

        /// <summary>
        /// 平均访问量
        /// </summary>
        public static double AverageCount
        {
            get
            {
                try
                {
                    return RedisHelper.Get<double>("Interview:ViewCount") / RedisHelper.Get<double>("Interview:RunningDays");
                }
                catch
                {
                    return 1;
                }
            }
        }

        /// <summary>
        /// 网站启动时间
        /// </summary>
        public static DateTime StartupTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 类型映射
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T Mapper<T>(this object source) where T : class => AutoMapper.Mapper.Map<T>(source);

        /// <summary>
        /// 上传图片到新浪图床
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        public static (string, bool) UploadImage(string file)
        {
            string ext = Path.GetExtension(file);
            if (!File.Exists(file))
            {
                return ("", false);
            }
            long fileLength = new FileInfo(file).Length;
            if (fileLength > 5 * 1024 * 1024)
            {
                if (!ext.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase))
                {
                    return ("", false);
                }

                using (var stream = File.OpenRead(file))
                {
                    string newfile = Path.Combine(Environment.GetEnvironmentVariable("temp"), "".CreateShortToken(16) + ext);
                    using (FileStream fs = new FileStream(newfile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    {
                        ImageUtilities.CompressImage(stream, fs, 100, 5120);
                        return UploadImage(newfile);
                    }
                }
            }

            string[] apis =
            {
                "https://tu.xiangkanju.com/Zs_UP.php?type=multipart",
                "https://api.yum6.cn/sinaimg.php?type=multipart",
            };
            int index = 0;
            string url = string.Empty;
            bool success = false;
            for (int i = 0; i < apis.Length; i++)
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
                    httpClient.DefaultRequestHeaders.Referrer = new Uri(apis[i]);
                    using (var stream = File.OpenRead(file))
                    {
                        using (var bc = new StreamContent(stream))
                        {
                            bc.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                            {
                                FileName = "".MDString() + ext,
                                Name = "file"
                            };
                            using (var content = new MultipartFormDataContent
                            {
                                bc
                            })
                            {
                                var code = httpClient.PostAsync(apis[index], content).ContinueWith(t =>
                                {
                                    if (t.IsCanceled || t.IsFaulted)
                                    {
                                        return 0;
                                    }

                                    var res = t.Result;
                                    if (res.IsSuccessStatusCode)
                                    {
                                        try
                                        {
                                            string s = res.Content.ReadAsStringAsync().Result;
                                            var token = JObject.Parse(s);
                                            switch (index)
                                            {
                                                case 0:
                                                    s = (string)token["code"];
                                                    if (s.Equals("200"))
                                                    {
                                                        url = (string)token["pid"];
                                                        return 1;
                                                    }

                                                    return 2;
                                                case 1:
                                                    s = (string)token["code"];
                                                    if (s.Equals("200"))
                                                    {
                                                        url = ((string)token["url"]).Replace("thumb150", "large");
                                                        return 1;
                                                    }

                                                    return 2;
                                            }
                                            if (url.Length < 40)
                                            {
                                                return 2;
                                            }
                                        }
                                        catch
                                        {
                                            return 2;
                                        }
                                    }

                                    return 2;
                                }).Result;
                                if (code == 1)
                                {
                                    success = true;
                                    break;
                                }

                                if (code == 0 || code == 2)
                                {
                                    index++;
                                    if (index == apis.Length)
                                    {
                                        Console.WriteLine("所有上传接口都挂掉了，重试sm.ms图床");
                                        return UploadImageFallback(file);
                                    }

                                    Console.WriteLine("正在准备重试图片上传接口：" + apis[index]);
                                    continue;
                                }

                                Console.WriteLine("准备重试上传图片");
                            }
                        }
                    }
                }
            }

            return (url.Replace("/thumb150/", "/large/"), success);
        }

        /// <summary>
        /// 上传图片到sm图床
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        private static (string, bool) UploadImageFallback(string file)
        {
            string url = string.Empty;
            bool success = false;
            string ext = Path.GetExtension(file);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
                using (var stream = File.OpenRead(file))
                {
                    using (var bc = new StreamContent(stream))
                    {
                        bc.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "".CreateShortToken() + ext,
                            Name = "smfile"
                        };
                        using (var content = new MultipartFormDataContent
                        {
                            bc
                        })
                        {
                            var code = httpClient.PostAsync("https://sm.ms/api/upload?inajax=1&ssl=1", content).ContinueWith(t =>
                            {
                                if (t.IsCanceled || t.IsFaulted)
                                {
                                    return 0;
                                }

                                var res = t.Result;
                                if (res.IsSuccessStatusCode)
                                {
                                    string s = res.Content.ReadAsStringAsync().Result;
                                    var token = JObject.Parse(s);
                                    url = (string)token["data"]["url"];
                                    return 1;
                                }

                                return 2;
                            }).Result;
                            if (code == 1)
                            {
                                success = true;
                            }
                        }
                    }
                }
            }

            if (success)
            {
                return (url, success);
            }

            return UploadImageFallback2(file);
        }

        /// <summary>
        /// 上传图片到百度图床
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        private static (string, bool) UploadImageFallback2(string file)
        {
            string url = string.Empty;
            bool success = false;
            string ext = Path.GetExtension(file);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
                using (var stream = File.OpenRead(file))
                {
                    using (var bc = new StreamContent(stream))
                    {
                        bc.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "".CreateShortToken() + ext,
                            Name = "uploaded_file[]"
                        };
                        using (var content = new MultipartFormDataContent
                        {
                            bc
                        })
                        {
                            var code = httpClient.PostAsync("https://upload.cc/image_upload", content).ContinueWith(t =>
                            {
                                if (t.IsCanceled || t.IsFaulted)
                                {
                                    return 0;
                                }

                                var res = t.Result;
                                if (res.IsSuccessStatusCode)
                                {
                                    string s = res.Content.ReadAsStringAsync().Result;
                                    var token = JObject.Parse(s);
                                    if ((int)token["code"] == 100)
                                    {
                                        url = "https://upload.cc/" + (string)token["success_image"][0]["url"];
                                        return 1;
                                    }
                                }

                                return 2;
                            }).Result;
                            if (code == 1)
                            {
                                success = true;
                            }
                        }
                    }
                }
            }

            if (success)
            {
                return (url, success);
            }

            return UploadImageFallback3(file);
        }

        /// <summary>
        /// 上传图片到百度图床
        /// </summary>
        /// <param name="file">文件名</param>
        /// <returns></returns>
        private static (string, bool) UploadImageFallback3(string file)
        {
            string url = string.Empty;
            bool success = false;
            string ext = Path.GetExtension(file);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
                using (var stream = File.OpenRead(file))
                {
                    using (var bc = new StreamContent(stream))
                    {
                        bc.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "".CreateShortToken() + ext,
                            Name = "img"
                        };
                        using (var content = new MultipartFormDataContent
                        {
                            bc
                        })
                        {
                            var code = httpClient.PostAsync("http://upload.otar.im/api/upload/imgur", content).ContinueWith(t =>
                            {
                                if (t.IsCanceled || t.IsFaulted)
                                {
                                    return 0;
                                }

                                var res = t.Result;
                                if (res.IsSuccessStatusCode)
                                {
                                    string s = res.Content.ReadAsStringAsync().Result;
                                    var token = JObject.Parse(s);
                                    url = (string)token["url"];
                                    return 1;
                                }

                                return 2;
                            }).Result;
                            if (code == 1)
                            {
                                success = true;
                            }
                        }
                    }
                }
            }
            return (url, success);
        }

        /// <summary>
        /// 替换图片地址
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string ReplaceImgSrc(string content)
        {
            var srcs = content.MatchImgSrcs();
            foreach (string src in srcs)
            {
                if (!src.StartsWith("http"))
                {
                    var path = Path.Combine(AppContext.BaseDirectory, src.Replace("/", @"\").Substring(1));
                    if (File.Exists(path))
                    {
                        var (url, success) = UploadImage(path);
                        if (success)
                        {
                            content = content.Replace(src, url);
                            BackgroundJob.Enqueue(() => File.Delete(path));
                        }
                    }
                }
            }
            return content;
        }

        /// <summary>
        /// 是否是机器人访问
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public static bool IsRobot(this HttpRequest req)
        {
            return req.Headers[HeaderNames.UserAgent].ToString().Contains(new[]
            {
                "DNSPod",
                "Baidu",
                "spider",
                "Python",
                "bot"
            });
        }
    }
}