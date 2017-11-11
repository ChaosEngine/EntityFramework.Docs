﻿using EFGetStarted.AspNetCore.ExistingDb;
using EFGetStarted.AspNetCore.ExistingDb.Controllers;
using EFGetStarted.AspNetCore.ExistingDb.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Integration
{
	static class Extensions
	{
		public static Dictionary<string, string> ToDictionary(this object myObj)
		{
			return myObj.GetType()
				.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
				.Select(pi => new { Name = pi.Name, Value = pi.GetValue(myObj, null)?.ToString() })
				.Union(
					myObj.GetType()
						.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
						.Select(fi => new { Name = fi.Name, Value = fi.GetValue(myObj)?.ToString() })
				)
				.ToDictionary(ks => ks.Name, vs => vs.Value);
		}
	}

	/// <summary>
	/// http://geeklearning.io/asp-net-core-mvc-testing-and-the-synchronizer-token-pattern/
	/// http://www.stefanhendriks.com/2016/05/11/integration-testing-your-asp-net-core-app-dealing-with-anti-request-forgery-csrf-formdata-and-cookies/
	/// </summary>
	static class PostRequestHelper
	{
		public static string ExtractAntiForgeryToken(string htmlResponseText)
		{
			if (htmlResponseText == null) throw new ArgumentNullException("htmlResponseText");

			Match match = Regex.Match(htmlResponseText, @"\<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" \/\>");
			return match.Success ? match.Groups[1].Captures[0].Value : null;
		}

		public static async Task<string> ExtractAntiForgeryToken(HttpResponseMessage response)
		{
			string responseAsString = await response.Content.ReadAsStringAsync();
			return await Task.FromResult(ExtractAntiForgeryToken(responseAsString));
		}

		/// <summary>
		/// Inspired from:
		/// https://github.com/aspnet/Mvc/blob/538cd9c19121f8d3171cbfddd5d842cbb756df3e/test/Microsoft.AspNet.Mvc.FunctionalTests/TempDataTest.cs#L201-L202
		/// </summary>
		/// <param name="response"></param>
		/// <returns></returns>
		public static IEnumerable<KeyValuePair<string, string>> ExtractCookiesFromResponse(HttpResponseMessage response)
		{
			if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> values))
			{
				var result = new KeyValuePair<string, string>[values.Count()];
				var cookie_jar = SetCookieHeaderValue.ParseList(values.ToList());
				int i = 0;
				foreach (var cookie in cookie_jar)
				{
					result[i] = new KeyValuePair<string, string>(cookie.Name.Value, cookie.Value.Value);
					i++;
				}
				return result;
			}
			else
				return new KeyValuePair<string, string>[0];
		}

		public static HttpRequestMessage PutCookiesOnRequest(HttpRequestMessage newHttpRequestMessage, IEnumerable<KeyValuePair<string, string>> cookies)
		{
			foreach (var kvp in cookies)
			{
				newHttpRequestMessage.Headers.Add("Cookie", new CookieHeaderValue(kvp.Key, kvp.Value).ToString());
			}

			return newHttpRequestMessage;
		}

		public static HttpRequestMessage CopyCookiesFromResponse(HttpRequestMessage newHttpRequestMessage, HttpResponseMessage getResponse)
		{
			return PutCookiesOnRequest(newHttpRequestMessage, ExtractCookiesFromResponse(getResponse));
		}

		public static HttpRequestMessage Create(string routePath, IEnumerable<KeyValuePair<string, string>> formPostBodyData)
		{
			var newHttpRequestMessage = new HttpRequestMessage(HttpMethod.Post, routePath)
			{
				Content = new FormUrlEncodedContent(formPostBodyData)
			};
			return newHttpRequestMessage;
		}

		public static HttpRequestMessage CreateHttpRequestMessageWithCookiesFromResponse(string routePath,
			IEnumerable<KeyValuePair<string, string>> formPostBodyData, HttpResponseMessage getResponse)
		{
			var newHttpRequestMessage = Create(routePath, formPostBodyData);
			return CopyCookiesFromResponse(newHttpRequestMessage, getResponse);
		}

		public static void CreateFormUrlEncodedContentWithCookiesFromResponse(HttpHeaders headers, HttpResponseMessage getResponse)
		{
			var cookies = ExtractCookiesFromResponse(getResponse);

			foreach (var kvp in cookies)
			{
				headers.Add("Cookie", new CookieHeaderValue(kvp.Key, kvp.Value).ToString());
			}
		}
	}

	[Collection(nameof(TestServerCollection))]
	public class HomePage
	{
		private readonly TestServerFixture<Startup> _fixture;
		private readonly HttpClient _client;

		public HomePage(TestServerFixture<Startup> fixture)
		{
			_fixture = fixture;
			_client = fixture.Client;
		}

		[Fact]
		public async Task Index()
		{
			// Arrange
			// Act
			using (HttpResponseMessage response = await _client.GetAsync("/"))
			{
				// Assert
				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				Assert.Contains("<title>Home Page - EFGetStarted.AspNetCore.ExistingDb</title>", responseString);
				Assert.Contains("<h2>Links</h2>", responseString);
			}
		}

		[Fact]
		public async Task ErrorHandlerTest()
		{
			// Arrange
			var data = new Dictionary<string, string>
			{
				{ "action", "exception" }
			};
			using (var content = new FormUrlEncodedContent(data))
			{
				// Act
				using (var response = await _client.PostAsync("/DeesNotExist/FooBar", content))
				{
					// Assert
					Assert.NotNull(response);
					Assert.False(response.IsSuccessStatusCode);
					Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
				}
			}
		}

		[Fact]
		public async Task UnintentionalErr()
		{
			// Arrange
			var data = new Dictionary<string, string>
			{
				{ "action", "exception" }
			};
			using (var content = new FormUrlEncodedContent(data))
			{
				// Act
				using (var response = await _client.PostAsync($"/Home/{nameof(HomeController.UnintentionalErr)}", content))
				{
					// Assert
					Assert.NotNull(response);
					Assert.False(response.IsSuccessStatusCode);
					Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
					var responseString = await response.Content.ReadAsStringAsync();
					Assert.Contains("Exception: test exception", responseString);
				}
			}
		}

		[Fact]
		public async Task ClientsideLog()
		{
			// Arrange
			var data = new Dictionary<string, string>
			{
				{ "level", LogLevel.Warning.ToString() },
				{ "message", "some message" },
				{ "url", "http://localhost/sourceURL" },
				{ "line", 2.ToString() },
				{ "col", 1.ToString() },
				{ "error", "some error" },
			};
			//// Serialize our concrete class into a JSON String
			//var stringPayload = JsonConvert.SerializeObject(payload);
			//// Wrap our JSON inside a StringContent which then can be used by the HttpClient class
			//var content = new StringContent(stringPayload, Encoding.UTF8, "application/json");
			using (var content = new FormUrlEncodedContent(data))
			{
				// Act
				using (var response = await _client.PostAsync($"/Home/{nameof(HomeController.ClientsideLog)}", content))
				{
					// Assert
					response.EnsureSuccessStatusCode();
					Assert.NotNull(response);
					Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				}
			}
		}
	}

	[Collection(nameof(TestServerCollection))]
	public class HashesDataTablePage
	{
		private readonly TestServerFixture<Startup> _fixture;
		private readonly HttpClient _client;

		public HashesDataTablePage(TestServerFixture<Startup> fixture)
		{
			_fixture = fixture;
			_client = fixture.Client;
		}

		[Fact]
		public async Task Index()
		{
			// Arrange
			// Act
			using (HttpResponseMessage response = await _client.GetAsync($"/{HashesDataTableController.ASPX}/"))
			{
				// Assert
				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				Assert.Contains("<button id=\"btninfo\" class=\"btn btn-default\" type=\"button\">" +
					"<i class=\"glyphicon glyphicon-info-sign\"></i>&nbsp;row info</button>",
					responseString);
				Assert.Contains("data-page-list=\"[5,10,20,50,500,2000]\"", responseString);
			}
		}

		[Fact]
		public async Task Load_Valid()
		{
			if (_fixture.DBKind == "sqlite") return;//pass on fake DB with no data


			// Arrange
			var data = new HashesDataTableLoadInput
			{
				Sort = "Key",
				Order = "desc",
				Search = "kawa",
				Limit = 5,
				Offset = 1,
			}.ToDictionary();
			using (var content = new FormUrlEncodedContent(data))
			{
				var queryString = await content.ReadAsStringAsync();
				// Act
				using (HttpResponseMessage response = await _client.GetAsync($"/{HashesDataTableController.ASPX}/{nameof(HashesDataTableController.Load)}?{queryString}", HttpCompletionOption.ResponseContentRead))
				{
					// Assert
					Assert.NotNull(response);
					response.EnsureSuccessStatusCode();
					Assert.Equal(HttpStatusCode.OK, response.StatusCode);

					var jsonString = await response.Content.ReadAsStringAsync();
					var typed_result = new
					{
						total = 1,
						rows = new ThinHashes[] { }
					};

					// Deserialize JSON String into concrete class
					var deserialized = JsonConvert.DeserializeObject(jsonString, typed_result.GetType()) as dynamic;
					Assert.IsType(typed_result.GetType(), deserialized);
					Assert.IsAssignableFrom<IEnumerable<ThinHashes>>(deserialized.rows);
					Assert.True(deserialized.rows.Length == 5);
					Assert.True(deserialized.total > 0);
					Assert.NotNull((deserialized.rows as ThinHashes[]).FirstOrDefault(r => r.Key.StartsWith("kawa")));
				}
			}
		}

		[Fact]
		public async Task Load_Invalid()
		{
			// Arrange
			var data = new HashesDataTableLoadInput
			{
				Sort = "dead",
				Order = "string",
				Search = "is",
				Limit = 0xDEAD,
				Offset = 0xBEEF,
			}.ToDictionary();
			using (var content = new FormUrlEncodedContent(data))
			{
				var queryString = await content.ReadAsStringAsync();
				// Act
				using (HttpResponseMessage response =
					await _client.GetAsync($"/{HashesDataTableController.ASPX}/{nameof(HashesDataTableController.Load)}?{queryString}",
					HttpCompletionOption.ResponseContentRead))
				{
					// Assert
					Assert.NotNull(response);
					Assert.False(response.IsSuccessStatusCode);
					Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				}
			}
		}
	}

	[Collection(nameof(TestServerCollection))]
	public class BlogsPage
	{
		private readonly TestServerFixture<Startup> _fixture;
		private readonly HttpClient _client;

		public BlogsPage(TestServerFixture<Startup> fixture)
		{
			_fixture = fixture;
			_client = fixture.Client;
		}

		[Fact]
		public async Task Show_Index()
		{
			if (_fixture.DBKind == "sqlite") return;//pass on fake DB with no data

			
			// Arrange
													// Act
			using (HttpResponseMessage response = await _client.GetAsync($"/{BlogsController.ASPX}/"))
			{
				// Assert
				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				Assert.Contains("<title>Blogs - EFGetStarted.AspNetCore.ExistingDb</title>", responseString);
				Assert.Contains("function FormSubmit(form)", responseString);
			}
		}

		[Fact]
		public async Task Show_Create()
		{
			// Arrange
			// Act
			using (HttpResponseMessage response = await _client.GetAsync($"/{BlogsController.ASPX}/{nameof(BlogsController.Create)}/"))
			{
				// Assert
				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				Assert.Contains("<title>New Blog - EFGetStarted.AspNetCore.ExistingDb</title>", responseString);
				Assert.Contains("<label class=\"col-md-2 control-label\" for=\"Url\">Url</label>", responseString);
			}
		}

		[Fact]
		public async Task Blog_CRUD_Test()
		{
			if (_fixture.DBKind == "sqlite") return;//pass on fake DB with no data


			// Arrange
			string antiforgery_token;
			List<KeyValuePair<string, string>> data;
			using (var create_get_response = await _client.GetAsync($"/{BlogsController.ASPX}/{nameof(BlogsController.Create)}/",
				HttpCompletionOption.ResponseContentRead))
			{
				// Assert
				create_get_response.EnsureSuccessStatusCode();
				antiforgery_token = await PostRequestHelper.ExtractAntiForgeryToken(create_get_response);

				// Arrange
				var now = DateTime.Now;
				data = new Blog
				{
					BlogId = 0,
					Post = new[] { new Post { Content = $"aaaa {now.ToString()}", Title = "titla" } },
					Url = $"http://www.inernetAt-{now.Year}-{now.Month}-{now.Day}.com/Content{now.Hour}-{now.Minute}-{now.Second}"
				}.ToDictionary().ToList();
				data.Add(new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery_token));

				using (var formPostBodyData = new FormUrlEncodedContent(data))
				{
					PostRequestHelper.CreateFormUrlEncodedContentWithCookiesFromResponse(formPostBodyData.Headers, create_get_response);
					// Act
					using (var redirect = await _client.PostAsync($"/{BlogsController.ASPX}/{nameof(BlogsController.Create)}/", formPostBodyData))
					{
						// Assert
						Assert.NotNull(redirect);
						Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
						Assert.Equal($"/{BlogsController.ASPX}", redirect.Headers.GetValues("Location").FirstOrDefault());
					}
				}


				int last_inserted_id;
				string last_inserted_ProtectedID;
				using (var index_response = await _client.GetAsync($"/{BlogsController.ASPX}/", HttpCompletionOption.ResponseContentRead))
				{
					var responseString = await index_response.Content.ReadAsStringAsync();
					MatchCollection matches = Regex.Matches(responseString, @"\<form method=""post"" class=""form-horizontal"" data-id=""([0-9].*)""\>");
					Assert.NotEmpty(matches);
					var ids = new List<int>(matches.Count);
					foreach (Match m in matches)
					{
						var match_count = m.Success ? m.Groups[1].Captures.Count : 0;
						Assert.True(match_count > 0);
						var id = int.Parse(m.Groups[1].Captures[match_count - 1].Value);
						ids.Add(id);
					}
					last_inserted_id = ids.OrderByDescending(_ => _).First();
					Match match = Regex.Match(responseString, $@"\<input type=""hidden"" id=""ProtectedID_{last_inserted_id}"" name=""ProtectedID"" value=""([^""]+)"" \/\>");
					Assert.True(match.Success && match.Groups[1].Captures.Count > 0);
					last_inserted_ProtectedID = match.Groups[1].Captures[0].Value;
				}

				data = new DecoratedBlog
				{
					BlogId = last_inserted_id,
					ProtectedID = last_inserted_ProtectedID,
				}.ToDictionary().ToList();
				data.Add(new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery_token));

				using (var formPostBodyData = new FormUrlEncodedContent(data))
				{
					PostRequestHelper.CreateFormUrlEncodedContentWithCookiesFromResponse(formPostBodyData.Headers, create_get_response);
					using (var response = await _client.PostAsync($"/{BlogsController.ASPX}/{nameof(BlogActionEnum.Delete)}/{last_inserted_id}/true",
						formPostBodyData))
					{
						Assert.NotNull(response);
						response.EnsureSuccessStatusCode();
						Assert.Contains("application/json", response.Content.Headers.GetValues("Content-Type").FirstOrDefault());
						Assert.Equal("\"deleted\"", await response.Content.ReadAsStringAsync());
					}
				}
			}//end using (var create_get_response
		}
	}
}