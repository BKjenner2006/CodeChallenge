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
        //Make sure the cookie is established before every request, need this for keeping track of user score
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
            GuessHandler handler = new GuessHandler();

            StackOverflowSearchVM searchResult = handler.GetRecentGuesses();

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

        //Submitting the question detail model to this action to maintain the randomized answer sort order
        public async Task<ActionResult> SubmitGuess(StackOverflowQuestionVM vm)
        {
            GuessHandler guessHandler = new GuessHandler();
            var answerID = vm.answers.FirstOrDefault(a => a.selected_answer == true).answer_id;
            GuessResult guessResult = guessHandler.SubmitGuess(answerID, vm.question_id, vm.title, vm.creation_date);

            StackOverflowDetailsVM result = await CallStackOverflow<StackOverflowDetailsVM>("questions", vm.question_id.ToString(), "order=desc&sort=activity&site=stackoverflow&filter=!-*jbN-o8P3E5");
            var question = result.items.FirstOrDefault();
            vm.body = question.body;

            foreach (var answer in vm.answers)
            {
                answer.body = question.answers.FirstOrDefault(a => a.answer_id == answer.answer_id).body;
                answer.creation_date = question.answers.FirstOrDefault(a => a.answer_id == answer.answer_id).creation_date;
                answer.selected_answer = answer.answer_id == answerID;
                answer.correct_answer = answer.answer_id == question.accepted_answer_id;
                answer.guess_count = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answer.answer_id)?.GuessCount ?? 0;
                answer.guess_percentage = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answer.answer_id)?.GuessPercentage ?? 0;
            }

            return PartialView("_QuestionDetails", vm);
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