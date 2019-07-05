using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace FaceDetection
{
    class Program
    {
        // Global variables for Azure resource access
        const string subscriptionKey = "e79135702bee4406a3853c28632f5711";
        const string uriBase = "https://eastus.api.cognitive.microsoft.com/face/v1.0/";
        private static HttpClient client;

        static void Main(string[] args)
        {
            client = new HttpClient();

            client.DefaultRequestHeaders.Add(
                "Ocp-Apim-Subscription-Key", subscriptionKey);

            // Get the path and filename to process from the user.
            Console.Write(
                "Enter the directory path to an image with faces that you wish to analyze: ");
            string imageFilePath = Console.ReadLine();

            Console.WriteLine(
                "Listing person groups within Azure resource");
            ListPersonGroups();

            Console.Write(
                "Enter the group of people you want to identify, will create a group if it doesn't exist: ");
            string personGroupName = Console.ReadLine();
            CreatePersonGroup(personGroupName);

            Console.Write(
                "Enter the name of the person: ");
            string personName = Console.ReadLine();
            AddPersonToGroup(personName, personGroupName);

            if (Directory.Exists(imageFilePath))
            {
                try
                {
                    foreach (string path in Directory.GetFiles(imageFilePath))
                    {
                        Console.WriteLine(path); // Getpath of each file in the directory
                        Console.WriteLine(Path.GetFileNameWithoutExtension(path));  // Get file name (person's name)
                        // Create person in group and add facial image to person?
                        //AddFaceToPerson(personName, personGroupName, path);

                        //Train
                        string uri = uriBase + "persongroups/" + personGroupName + "/train";
                        client.PostAsync(uri, null);

                        HttpResponseMessage response = client.GetAsync(uriBase + "persongroups/" + personGroupName + "/training").Result;
                        string status;

                        var message = response.Content.ReadAsStringAsync().Result;
                        JObject result = JsonConvert.DeserializeObject<JObject>(message);
                        status = result["status"].ToString();

                        while (!String.Equals(status, "succeeded"))
                        {
                            Console.WriteLine("Training not complete");
                            response = client.GetAsync(uriBase + "persongroups/" + personGroupName + "/training").Result;
                            message = response.Content.ReadAsStringAsync().Result;
                            result = JsonConvert.DeserializeObject<JObject>(message);
                            status = result["status"].ToString();
                        }

                        Console.WriteLine("Fetching analysis after picture added");
                        MakeAnalysisRequest(path);
                    }
                    Console.WriteLine("\nWait a moment for the results to appear.\n");
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n" + e.Message + "\nPress Enter to exit...\n");
                }
            }
            else
            {
                Console.WriteLine("\nInvalid file path.\nPress Enter to exit...\n");
            }
            Console.ReadLine();
        }

        static async void MakeAnalysisRequest (string imageFilePath)
        {
            // Request parameters. A third optional parameter is "details".
            string requestParameters = "returnFaceId=true&returnFaceLandmarks=false" +
                "&returnFaceAttributes=age,gender,smile,facialHair,glasses,emotion";

            // Assemble the URI for the REST API Call.
            string uri = uriBase + "detect" + "?" + requestParameters;

            HttpResponseMessage response;

            byte[] byteData = GetImageAsByteArray(imageFilePath);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json"
                // and "multipart/form-data".
                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                Console.WriteLine("\nResponse:\n");
                Console.WriteLine(JsonPrettyPrint(contentString));
                Console.WriteLine("\nPress Enter to exit...");
            }
        }

        static void ListPersonGroups()
        {
            string uri = uriBase + "/persongroups";

            HttpResponseMessage response = client.GetAsync(uri).Result;

            string content = response.Content.ReadAsStringAsync().Result;

            Console.WriteLine(JsonPrettyPrint(content));
        }

        static async Task<HttpResponseMessage> GetPersonGroupById(string Id)
        {
            string uri = uriBase + "/persongroups/" + Id;

            return await client.GetAsync(uri);
        }

        static async void CreatePersonGroup(string Id)
        {
            string uri = uriBase + "/persongroups/" + Id;

            HttpResponseMessage response = GetPersonGroupById(Id).Result;

            // If person group does not exist create it
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                JObject content = new JObject
                {
                    ["name"] = Id
                };

                StringContent requestContent = new StringContent(JsonConvert.SerializeObject(content));
                requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                response = await client.PutAsync(uri, requestContent);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                }

            }
            else
            {
                Console.WriteLine("Person group with ID " + Id + " already exists");
            }
        }

        static string GetPersonId(string name, string groupId)
        {
            string uri = uriBase + "/persongroups/" + groupId + "/persons";
            HttpResponseMessage response = client.GetAsync(uri).Result;

            var message = response.Content.ReadAsStringAsync().Result;

            JArray result = JsonConvert.DeserializeObject<JArray>(message);
            
            foreach(JToken token in result)
            {
                if (token["name"].ToString() == name)
                {
                    return token["personId"].ToString();
                }
            }

            return String.Empty;
        }

        static async void AddPersonToGroup(string name, string groupId)
        {
            if (GetPersonId(name, groupId) == String.Empty)
            {
                string uri = uriBase + "/persongroups/" + groupId + "/persons";

                JObject content = new JObject
                {
                    ["name"] = name
                };

                StringContent requestContent = new StringContent(JsonConvert.SerializeObject(content));
                requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PostAsync(uri, requestContent);
            }
            else
            {
                Console.WriteLine("Person with ID " + name + " already exists...");
            }
        }

        static async void AddFaceToPerson(string name, string groupId, string imageFilePath)
        {
            string id = GetPersonId(name, groupId);
            string uri = uriBase + "/persongroups/" + groupId + "/persons/" + id + "/persistedFaces";

            HttpResponseMessage response;

            try {
                byte[] byteData = GetImageAsByteArray(imageFilePath);

                using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                Console.WriteLine("\nResponse:\n");
                Console.WriteLine(JsonPrettyPrint(contentString));
                Console.WriteLine("\nPress Enter to exit...");
            }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }

        #region Helper Methods
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }

        static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            json = json.Replace(Environment.NewLine, "").Replace("\t", "");

            StringBuilder sb = new StringBuilder();
            bool quote = false;
            bool ignore = false;
            int offset = 0;
            int indentLength = 3;

            foreach (char ch in json)
            {
                switch (ch)
                {
                    case '"':
                        if (!ignore) quote = !quote;
                        break;
                    case '\'':
                        if (quote) ignore = !ignore;
                        break;
                }

                if (quote)
                    sb.Append(ch);
                else
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', ++offset * indentLength));
                            break;
                        case '}':
                        case ']':
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', --offset * indentLength));
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', offset * indentLength));
                            break;
                        case ':':
                            sb.Append(ch);
                            sb.Append(' ');
                            break;
                        default:
                            if (ch != ' ') sb.Append(ch);
                            break;
                    }
                }
            }

            return sb.ToString().Trim();
        }
        #endregion
    }
}
