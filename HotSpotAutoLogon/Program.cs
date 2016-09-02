using System;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

static class HotSpotAutoLogon
{
    private static string _hostUrl = @"http://192.168.2.1";
    private static string _statusUrl = _hostUrl + @"/status";
    private static string _loginUrl = _hostUrl + @"/login?dst={0}&username={1}";

    private static HttpWebRequest _request;
    private static HttpWebResponse _response;
    private static CookieCollection _cookies = null;

    private static int _time_s = 20 * 60;
    private static int _time_ms = 1000;
    private static int _time_s_wait = 60 * 5;
    private static int _time_ms_wait = 1000;
    private static int _time_s_reset = 5 * 60;

    private static string _html = "";
    private static string _user = "";

    /// <summary>
    /// Main
    /// </summary>
    /// <param name="">wait time</param>
    public static void Main(string[] args)
    {
        if (args.Length > 0)
            _time_ms_wait = int.Parse(args[0]);
        if (args.Length > 1)
            _time_s_reset = int.Parse(args[1]);

        _html = null;

        while (true)
        {
            _time_s = _time_s_reset;
            LogWrite(string.Format("resetowanie czasu oczekiwania na {0} sekund", _time_s));

            //Sprawdź status
            _html = GetHtml(_statusUrl);

            if (_html != null)
            {
                //Sprawdź czy jesteś zalogowany
                if (HtmlGetUser(_html, ref _user))
                {
                    LogWrite("to jest strona logowania, a ty nie jesteś zalogowany, " + _user);
                    //Zaloguj się
                    _html = GetHtml(string.Format(_loginUrl, "", _user));
                }
                else
                {
                    LogWrite("to nie jest strona logowania");
                }

                if (_html != null)
                {
                    //Sprawdź ile zostało czasu
                    if (HtmlGetTime(_html, ref _time_s))
                        LogWrite("jesteś zalogowany " + _user + ", pozostało " + _time_s + " sekund do wylogowania");
                    else
                        LogWrite("to nie jest strona ze statusem, musisz się zalogować " + _user);
                }
            }//else
            ;//LogWrite("brak strony ze statusem");

            if (_time_s < 10) //<10s
            {
                _time_s_wait = 1;
                _time_ms = _time_ms_wait;
                LogWrite(string.Format("UWAGA: czas oczekiwania został zmniejszony do ({0} * {1}) = {2} milisekund",
                    _time_s_wait, _time_ms, _time_s_wait * _time_ms));
            }
            else //>=10s
                if (_time_s < 60) //<1min
            {
                _time_s_wait = _time_s / 2;
                _time_ms = 1000;
            }
            else
                    if (_time_s < 60 * 10) //<10min
            {
                _time_s_wait = _time_s / 2;
                _time_ms = 1000;
            }
            else //>10min
            {
                _time_s_wait = _time_s / 2;
                _time_ms = 1000;
            }

            LogWrite(string.Format("czekam {0} milisekund ({1:F1} sekund ({2:F2} minut))...",
                _time_s_wait * _time_ms,
                (_time_s_wait * _time_ms) / 1000.0,
                ((_time_s_wait * _time_ms) / 1000.0) / 60.0));

            Thread.Sleep(_time_s_wait * _time_ms);
        }

    }

    /// <summary>
    /// Pobierz stronę.
    /// </summary>
    /// <param name="url"></param>
    /// <returns>Strona html lub null(błąd)</returns>
    public static string GetHtml(string url)
    {
        string html = null;

        try
        {
            _request = (HttpWebRequest)WebRequest.Create(url);
            _request.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";
            _request.CookieContainer = new CookieContainer();
            _request.AllowAutoRedirect = false;

            //Bez ciasteczek nie uda nam się zalogować.
            if (_cookies != null)
            {
                LogWrite(string.Format("mam {0} ciasteczek", _cookies.Count));
                foreach (Cookie cookie in _cookies)
                {
                    _request.CookieContainer.SetCookies(new Uri(url), cookie.ToString());
                    LogWrite(string.Format("nowe ciasteczko: {0}", cookie.ToString()));
                    //_request.CookieContainer.Add(new Uri(url), _cookies); //Add doesn't work
                }

            }
            else
            {
                LogWrite("w tym momencie nie ma ciasteczek");
            }

            LogWrite(string.Format("pobieranie strony {0} ...", url));

            _response = _request.GetResponse() as HttpWebResponse;
            _cookies = _response.Cookies;

            LogWrite(string.Format("odpowiedź serwera: {0}({1}) - {2}",
                (int)_response.StatusCode, _response.StatusCode, _response.StatusDescription));

            StreamReader reader = new StreamReader(_response.GetResponseStream());
            html = reader.ReadToEnd();

            reader.Close();
            _response.Close();

            //Sprawdzamy czy wystąpiły przekierowania i podążamy za nimi.
            if (_response.StatusCode == HttpStatusCode.Redirect || _response.StatusCode == HttpStatusCode.RedirectMethod)
            {
                url = _response.Headers["Location"];
                LogWrite(string.Format("przekierowanie na stronę {0}", url));
                return GetHtml(url);
            }
        }
        catch (WebException we)
        {
            LogWrite("błąd podczas pobierania strony: " + we.Message);
            html = null;
        }
        catch (Exception ex)
        {
            LogWrite("błąd ogólny: " + ex.Message);
            html = null;
        }

        return html;
    }

    /// <summary>
    /// HtmlGetValue
    /// </summary>
    /// <param name="url"></param>
    /// <returns>value</returns>
    public static string HtmlGetValue(string html, string name)
    {
        string pattern = string.Format(@"{0}=(?<name>[^&""]+)", name);
        Regex regex = new Regex(pattern);
        if (regex.IsMatch(html))
        {
            return regex.Match(html).Groups["name"].ToString();
        }
        else
            return null;
    }

    /// <summary>
    /// GetValue
    /// </summary>
    /// <param name="html"></param>
    /// <param name="name"></param>
    /// <returns>value</returns>
    public static string HtmlGetNameValue(string html, string name)
    {
        string pattern = string.Format(@"name=""{0}"" value=""(?<value>[^""]*)""", name);
        Regex regex = new Regex(pattern);
        string value = null;
        if (regex.IsMatch(html))
        {
            Match match = regex.Match(html);
            value = match.Groups["value"].ToString();
        }
        return value;
    }

    /// <summary>
    /// LogWrite
    /// </summary>
    /// <param name="msg"></param>
    public static void LogWrite(string msg)
    {
        Console.WriteLine("{0}> {1}", DateTime.Now, msg);
    }

    /// <summary>
    /// HtmlGetUser - pobierz nazwę użytkownika
    /// </summary>
    /// <returns>true or false</returns>
    /// href="http://192.168.2.1/login?dst=&amp;username=T-00%3A21%3A63%3A92%3A99%3AB8">
    public static bool HtmlGetUser(string html, ref string user)
    {
        if (html.Contains("Zaloguj się naciskając"))
        {
            return (user = HtmlGetValue(html, "username")) != null;
        }
        return false;
    }

    /// <summary>
    /// HtmlGetTime - ile zostało czasu do zakończenia sesji
    /// </summary>
    /// <returns>true or false</returns>
    /// <br><div style="text-align: center;">Welcome trial user!</div><br>
    /// <tr><td align="right">connected / left:</td><td>30s / 19m30s</td></tr>
    /// <tr><td align="right">status refresh:</td><td>19m31s</td>
    /// 1m1s / 19m1s lub 1s / 19m59s lub 19m30s / 30s
    public static bool HtmlGetTime(string html, ref int time_s)
    {
        if (html.Contains("Welcome trial user!"))
        {
            string pattern = @"connected / left:\<\/td\>\<td\>[^\/]+\/ ((?<min>\d+)m)?((?<sec>\d+)s)?\<\/td\>";

            Regex regex = new Regex(pattern);
            string min = "0";
            string sec = "0";

            if (regex.IsMatch(html))
            {
                Match match = regex.Match(html);
                min = match.Groups["min"].ToString();
                sec = match.Groups["sec"].ToString();
                if (min == "") min = "0";
                if (sec == "") sec = "0";

                int t_s = int.Parse(sec);
                int t_m = int.Parse(min);
                time_s = t_m * 60 + t_s;

                return true;
            }
        }

        return false;
    }
}
