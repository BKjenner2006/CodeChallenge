using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using CodeChallenge.Models;
using CodeChallenge.Helpers;
using Newtonsoft.Json;

namespace CodeChallenge.Controllers
{
    public class HomeController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string userGuid = HttpContext.Request.Cookies.Get("userGuid")?.Value;

            if (String.IsNullOrEmpty(userGuid))
            {
                HttpCookie cookie = new HttpCookie("userGuid");
                cookie.Expires = DateTime.UtcNow.AddMonths(1);
                //cookie would not save when setting domain on localhost
                //options.Domain = Request.Host.ToUriComponent();
                cookie.Path = "/";
                cookie.HttpOnly = false;
                cookie.Value = Guid.NewGuid().ToString();
                Response.Cookies.Add(cookie);
            }
        }

        public async Task<ActionResult> Index()
        {
            StackOverflowSearchVM searchResult = await CallStackOverflow<StackOverflowSearchVM>("search", "advanced", "order=desc&sort=creation&accepted=True&answers=2&site=stackoverflow");

            return View(searchResult);
        }

        public ActionResult Guesses()
        {
            ViewData["Message"] = "Your application description page.";

            StackOverflowSearchVM searchResult = new StackOverflowSearchVM();

            return View(searchResult);
        }

        public ActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public ActionResult Privacy()
        {
            return View();
        }

        public async Task<ActionResult> SubmitGuess(int questionID, int answerID, string description, long questionDate)
        {
            GuessHandler guessHandler = new GuessHandler();
            GuessResult guessResult = guessHandler.SubmitGuess(answerID, questionID, description, questionDate);

            StackOverflowDetailsVM result = await CallStackOverflow<StackOverflowDetailsVM>("questions", questionID.ToString(), "order=desc&sort=activity&site=stackoverflow&filter=!-*jbN-o8P3E5");
            foreach (var answer in result.items.FirstOrDefault().answers)
            {
                answer.selected_answer = answer.answer_id == answerID;
                answer.guess_count = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answerID)?.GuessCount ?? 0;
                answer.guess_percentage = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answerID)?.GuessPercentage ?? 0;
            }

            return PartialView("_QuestionDetails", result.items.FirstOrDefault());
        }

        public async Task<ActionResult> LoadQuestionDetails(int questionID)
        {
            StackOverflowDetailsVM result = await CallStackOverflow<StackOverflowDetailsVM>("questions", questionID.ToString(), "order=desc&sort=activity&site=stackoverflow&filter=!-*jbN-o8P3E5");

            Random rng = new Random();
            //Small data sets, not concerned with time complexity of this sort
            result.items.FirstOrDefault().answers = result.items.FirstOrDefault().answers.OrderBy(a => rng.Next()).ToList();

            return PartialView("_QuestionDetails", result.items.FirstOrDefault());
        }

        //A generic function to call the StackOverflow api, controller is required, function and parameters are optional
        private async Task<T> CallStackOverflow<T>(string controller, string function, string parameters)
        {
            T responseObject = default(T);
            StringBuilder apiUrl = new StringBuilder();
            apiUrl.Append(controller);

            if (!String.IsNullOrEmpty(function))
                apiUrl.Append("/" + function);

            if (!String.IsNullOrEmpty(parameters))
                apiUrl.Append("?" + parameters);


            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.stackexchange.com/2.2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("GZIP"));

                HttpResponseMessage response = await client.GetAsync(apiUrl.ToString());
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        Stream responseStream = await response.Content.ReadAsStreamAsync();
                        using (var decompressed = new MemoryStream())
                        {
                            using (var gs = new GZipStream(responseStream, CompressionMode.Decompress))
                            {
                                gs.CopyTo(decompressed);
                                string json = Encoding.UTF8.GetString(decompressed.ToArray());
                                responseObject = JsonConvert.DeserializeObject<T>(json);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                }
            }

            return responseObject;
        }
    }
}