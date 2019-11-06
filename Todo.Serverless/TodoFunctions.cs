// Licensed under the MIT license. See LICENSE file in the samples root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Todo.Models;

namespace SignalRServerless
{
    public static class TodoFunctions
    {
        private const string DatabaseName = "signalrdemo";
        private const string CollectionName = "todoitems";
        private const string HubName = "todoQueue";
        private const string CosmosDbSetting = "CosmosDBConnectionString";

        private static readonly JsonSerializer _serializer = new JsonSerializer();

        /// <summary>
        /// Returns the html to be used on the client
        /// </summary>
        [FunctionName("app")]
        public static IActionResult GetApp(
            [HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "app")]HttpRequest req)
        {
            return new FileStreamResult(typeof(TodoFunctions).Assembly.GetManifestResourceStream("Todo.Serverless.jsclient.html"), "text/html");
        }

        /// <summary>
        /// Returns a list of todo items
        /// </summary>
        [FunctionName("todoItems")]
        public static IActionResult TodoItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "todo/item")]HttpRequest req,
            [CosmosDB(databaseName: DatabaseName, collectionName: CollectionName, ConnectionStringSetting = CosmosDbSetting, SqlQuery = "SELECT * FROM c ORDER BY c._ts desc", CreateIfNotExists = true)]IEnumerable<TodoItem> todoItems)
        {
            if (todoItems == null)
            {
                return new OkObjectResult(Enumerable.Empty<TodoItem>());
            }

            return new OkObjectResult(todoItems);
        }


        /// <summary>
        /// Returns a list of todo items
        /// </summary>
        [FunctionName("updateTodoItems")]
        public static async Task<IActionResult> UpdateTodoItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "PUT", Route = "todo/update")]HttpRequest req,
            [CosmosDB(databaseName: DatabaseName, collectionName: CollectionName, ConnectionStringSetting = CosmosDbSetting, CreateIfNotExists = true)] DocumentClient client)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);

            var itemToUpdate = Deserialize<TodoItem>(req.Body);

            if (string.IsNullOrEmpty(itemToUpdate.Id))
            {
                return new BadRequestResult();
            }

            await client.UpsertDocumentAsync(collectionUri, itemToUpdate);

            return new OkObjectResult(itemToUpdate);
        }

        /// <summary>
        /// Add todo item
        /// </summary>
        [FunctionName("addTodo")]
        public static async Task<IActionResult> AddTodo(
            [CosmosDB(databaseName: DatabaseName, collectionName: CollectionName, ConnectionStringSetting = CosmosDbSetting)]IAsyncCollector<TodoItem> todoItems,
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo/add")] HttpRequest req)
        {
            var itemToAdd = Deserialize<TodoItem>(req.Body);

            await todoItems.AddAsync(itemToAdd);

            return new NoContentResult();
        }

        /// <summary>
        /// Function that gets triggered when a change is noticed in the CosmosDB connection
        /// </summary>
        [FunctionName("CosmosTrigger")]
        public static async Task Run(
            [CosmosDBTrigger(databaseName: DatabaseName, collectionName: CollectionName, ConnectionStringSetting = CosmosDbSetting, CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
            [SignalR(HubName = HubName)]IAsyncCollector<SignalRMessage> signalRMessages)
        {
            if (documents != null && documents.Count > 0)
            {
                foreach (var document in documents)
                {
                    var todoItem = JsonConvert.DeserializeObject<TodoItem>(document.ToString());

                    await signalRMessages.AddAsync(new SignalRMessage
                    {
                        // The target has to match the target that is listed on all clients
                        Target = "todoItemsChanged",
                        Arguments = new[] { todoItem }
                    });
                }
            }
        }

        /// <summary>
        /// Returns a SignalR connection
        /// </summary>
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequest req,
            [SignalRConnectionInfo(HubName = HubName)]SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        /// <summary>
        /// Takes the stream and deserializes it to an object of type T
        /// </summary>
        private static T Deserialize<T>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return _serializer.Deserialize<T>(jsonReader);
            }
        }
    }
}
