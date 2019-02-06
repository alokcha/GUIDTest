using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using GUID;
using Xunit;
using GUID.Controllers;
using GUID.Models;
using System.Net;
using Newtonsoft.Json;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Collections.Generic;

namespace GUIDTest.Integration
{
    public class IntegrationTest
    {
        private readonly ITestOutputHelper output;
        private readonly HttpClient _client;
        private readonly DbContext _context;

        /// <summary>
        /// Initializing and test hosting the API
        /// </summary>
        /// <param name="output"></param>
        public IntegrationTest(ITestOutputHelper output)
        {
            this.output = output;
            // Set up server configuration
            var configuration = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(@"../../../../GUID/")).Build();
            // Create builder
            var builder = new WebHostBuilder().UseEnvironment("Testing").UseStartup<GUID.Startup>().UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5432;Username=postgres;Password=password;Database=GUIDCollection;");
            // Create test server
            var server = new TestServer(builder);
            this._client = server.CreateClient();
        }
        /// <summary>
        /// Helper method for Posting
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        async Task<HttpResponseMessage> Post(string guid, dynamic param)
        {
            var paramString = JsonConvert.SerializeObject(param);
            HttpContent contentPost = new StringContent(paramString, Encoding.UTF8, "application/json");
            guid = guid ?? "";
            var response = await _client.PostAsync($"http://localhost:5002/api/guid/{guid}", contentPost);
            return response;
        }

        async Task<GUIDData> TryPost(string guid, dynamic param)
        {
            var response = await Post(guid, param);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return (GUIDData)JsonConvert.DeserializeObject<GUID.Models.GUIDData>(jsonResponse);
        }

        /// <summary>
        /// Simple Test for Get Guid API
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async void TestGet()
        {
            var validExpiry = new DateTimeOffset(DateTime.Now.AddDays(100)).ToUnixTimeSeconds();

            // create a new guid
            string guid = Guid.NewGuid().ToString();
            // save the new guid to the api's store
            await TryPost(guid, new { guid = guid, expire = validExpiry, user = "T. Bone" });

            //HttpContent contentPost = new StringContent(paramString, Encoding.UTF8, "application/json");
            guid = guid ?? "";
            GUIDData guidObj = new GUIDData(guid);
            guidObj.expire = validExpiry;
            guidObj.user = "T. Bones";
            // attempt to Get the same guid
            var response = await _client.GetAsync($"http://localhost:5002/api/guid/{guid}");
            // test the GET API's response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<GUID.Models.GUIDData>(jsonResponse);
            // test for guid values
            Assert.Equal(guid, responseObj.guid);
        }

        /// <summary>
        /// Simple Test for Create and Update
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async void TestCreateUpdate()
        {
            var validExpiry = new DateTimeOffset(DateTime.Now.AddDays(200)).ToUnixTimeSeconds();

            // create a new guid
            string guid = Guid.NewGuid().ToString();
            // Call Create API with the new guid and a valid expiry time
            var responseObj = await TryPost(guid, new { guid = guid, expire = validExpiry, user = "A1. Aron" });
            // test if guid matches with the one in response
            Assert.Equal(guid, responseObj.guid);

            // Call Create API with no guid and a valid expiry time
            responseObj = await TryPost(null, new { expire = validExpiry, user = "O. Shannesy" });

            // test if guid in the response is a valid one
            Guid testGuid;
            Assert.True(Guid.TryParse(responseObj.guid, out testGuid));

            // Call Update API with the previous guid
            string newUser = "R. Smith";
            responseObj = await TryPost(responseObj.guid, new { guid = responseObj.guid, expire = validExpiry, user = newUser });
            // test if the user name is changed to the new one
            Assert.Equal(newUser, responseObj.user);
        }

        /// <summary>
        /// Simple Test for delete
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async void TestDelete()
        {
            var validExpiry = new DateTimeOffset(DateTime.Now.AddDays(200)).ToUnixTimeSeconds();

            // create a new guid
            string guid = Guid.NewGuid().ToString();
            // Call Create API with the new guid and a valid expiry time
            var responseObj = await TryPost(guid, new { guid = guid, expire = validExpiry, user = "T.B. Del" });
            // test if guid matches with the one in response
            Assert.Equal(guid, responseObj.guid);

            var response = await _client.DeleteAsync($"http://localhost:5002/api/guid/{guid}");
            // confirm the Delete API's response
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);


            // attempt to fetch the deleted guid
            response = await _client.GetAsync($"http://localhost:5002/api/guid/{guid}");
            // confirm the expected response
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        /// <summary>
        /// Test for Invalid Operations
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async void TestInvalidOps()
        {
            var validExpiry = new DateTimeOffset(DateTime.Now.AddDays(400)).ToUnixTimeSeconds();
            var inValidExpiry = new DateTimeOffset(DateTime.Now.AddDays(-600)).ToUnixTimeSeconds();

            // create a new guid
            string guid = Guid.NewGuid().ToString();
            // Call the Create API with the new guid and an expiry time that is already in the past - the guid data will not be created as the expiry time is in the past
            var response = await Post(guid, JsonConvert.SerializeObject(new { guid = guid, expire = inValidExpiry, user = "Tyrone Biggums" }));
            // confirm the response 
            Assert.Equal(HttpStatusCode.BadRequest.ToString(), response.StatusCode.ToString());

            // create a new guid
            guid = Guid.NewGuid().ToString();
            // Call Create API with the new guid and a valid expiry time
            var responseObj = await TryPost(guid, new { guid = guid, expire = validExpiry, user = "A1. Aron" });

            // Get Data with the same guid
            response = await _client.GetAsync($"http://localhost:5002/api/guid/{guid}");
            // test the GET API's response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            responseObj = JsonConvert.DeserializeObject<GUID.Models.GUIDData>(jsonResponse);
            // test if the getched guid matches withe the one created above
            Assert.Equal(guid, responseObj.guid);

            // Call Update API with above guid but the expiry time is in the past - the Guid data should get removed due to expired time
            response = await Post(guid, new { guid = guid, expire = inValidExpiry });

            // Get Data again with the same guid - should not get any data, as it was removed in the previous save with invalid expiry time
            response = await _client.GetAsync($"http://localhost:5002/api/guid/{guid}");
            // test the GET API's response
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        /// <summary>
        /// Test for Time expiring between requests
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async void TestTimeExpiry()
        {
            var justValidExpiry = new DateTimeOffset(DateTime.Now.AddSeconds(2)).ToUnixTimeSeconds();

            // create a new guid
            string guid = Guid.NewGuid().ToString();
            // Call the Create API with the new guid and an expiry time that is valid, but almost about to expire
            var responseObj = await TryPost(guid, new { guid = guid, expire = justValidExpiry, user = "R. Bolton" });
            // confirm the response 
            Assert.Equal(guid, responseObj.guid);

            // wait for the this guid's data to expire
            await Task.Delay(3000);

            // Update data for the same guid, but do not pass any new expire time
            var response = await Post(guid, JsonConvert.SerializeObject(new { user = "D. Shade" }));
            // check for expected resonse - the record should get removed
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}